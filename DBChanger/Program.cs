using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mono.Data.Sqlite;
using System.Data;
using System.IO;
using System.Data.Common;

namespace DBChanger
{
    class Program
    {
        static SqliteConnection tShockDb;
        static SqliteConnection minigamesDb;

        static void Main(string[] args)
        {
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            InfoMessage("[DBChanger] Подключение к tshock.sqlite . . .");
            tShockDb = await GetConnection(Path.Combine("tshock", "tshock.sqlite"));
            if (tShockDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Подключение к Minigames.sqlite . . .");
            minigamesDb = await GetConnection(Path.Combine("tshock", "Minigames", "Minigames.sqlite"));
            if (minigamesDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Получение списка игроков . . .");
            var list = await GetList();
            if (list == null || list.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Проверка списка игроков . . .");
            if (!await CheckList(list))
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Удаление отобранных игроков . . .");
            foreach (var x in list)
            {
                if (await Delete(tShockDb, "Users", x.name))
                    x.deleted++;
                if (await Delete(minigamesDb, "Players", x.name))
                    x.deleted++;
            }

            string results = " --- --- --- РЕЗУЛЬТАТЫ ЧИСТКИ ЗАБРОШЕННЫХ АККАУНТОВ --- --- ---" + '\n' +
                             '\n' +
                             '\n' +
                             '\n' +
                             " --- Условия удаления ---" + '\n' +
                             "Группа: default" + '\n' +
                             "Инвентарь: пустой" + '\n' +
                             "Баланс: не больше 100" + '\n' +
                             "Последний логин: больше 3 месяцев назад" + '\n' +
                             '\n' +
                             '\n' +
                             '\n' +
                             " --- Общие сведения ---" + '\n' +
                             $"Удалено: {list.Count(x => x.deleted == 2)}" + '\n' +
                             $"Удалено не полностью (1 ошибка): {list.Count(x => x.deleted == 1)}" + '\n' +
                             $"Не удалено (2 ошибки): {list.Count(x => x.deleted == 0)}" + '\n' +
                             '\n' +
                             '\n' +
                             '\n' +
                             " --- Список отобранных аккаунтов ---" + '\n' +
                             "  *  Name - Last Login - Balance - Discord - Status" + '\n' +
                             string.Join("\n", list.Select(x => $"  *  {x.name} - {x.lastLogin.ToString()} - {x.balance} - {x.discord} - {x.status}")) + '\n' +
                             '\n' +
                             '\n' +
                             '\n' +
                             $"Дата: {DateTime.Now.ToString()}, Автор программы: Диагенов Михаил";


            File.WriteAllText($"{Environment.CurrentDirectory}\\DBChanger results.txt", results);
            SuccessMessage("[DBChanger] Готово!");
            await Task.Delay(-1);
        }

        static async Task<SqliteConnection> GetConnection(string path)
        {
            if (!File.Exists(path))
            {
                ErrorMessage($"[File] Error: файл {path} не найден.");
                return null;
            }

            var conn = new SqliteConnection($"uri=file://{path};Version=3;");
            using (var cmd = new SqliteCommand { Connection = conn })
            {
                try
                {
                    if (conn.State == ConnectionState.Open)
                        conn.Close();
                    await conn.OpenAsync();
                    SuccessMessage("[Database] Успешное подключение!");
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] Error: {ex.Message}.");
                    return null;
                }
            }
            return conn;
        }

        static async Task<List<AccountInfo>> GetList()
        {
            var list = new List<AccountInfo>();
            using (var cmd = tShockDb.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `Users` WHERE `Usergroup`='default';";
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var lastLogin = r.IsDBNull(6) ? new DateTime() : DateTime.Parse(r.GetString(6)).ToLocalTime();
                            if ((DateTime.Now - lastLogin).TotalDays > 90)
                                list.Add(new AccountInfo
                                {
                                    name = r.GetString(1),
                                    lastLogin = lastLogin
                                });
                        }
                        return list;
                    }
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.Message}");
                }
                catch (InvalidCastException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.Message}");
                }
            }
            return null;
        }

        static async Task<bool> CheckList(List<AccountInfo> list)
        {
            using (var cmd = minigamesDb.CreateCommand())
            {
                try
                {
                    foreach (var x in new List<AccountInfo>(list))
                    {
                        cmd.CommandText = $"SELECT * FROM `Players` WHERE `Username`='{x.name.Replace("'", "''")}';"; //  ''/'
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                x.balance = r.GetInt32(1);
                                if (x.balance > 100 || r.GetString(2) != "{}")
                                {
                                    list.Remove(x);
                                    continue;
                                }
                                x.discord = r.IsDBNull(3) ? "0" : r.GetString(3);
                                if (string.IsNullOrWhiteSpace(x.discord))
                                    x.discord = "0";
                            }
                        }
                    }
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.Message}");
                    return false;
                }
                catch (ArgumentException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.Message}");
                    return false;
                }
                catch (InvalidCastException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.Message}");
                    return false;
                }
            }
            return list.Count > 0;
        }

        static async Task<bool> Delete(SqliteConnection db, string tableName, string username)
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = $"DELETE FROM `{tableName}` WHERE `Username`='{username.Replace("'", "''")}';";
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                    return true;
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [{tableName}] Error: {ex.Message}");
                    return false;
                }
            }
        }

        static void ErrorMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"[{DateTime.Now.ToString()}] " + message);
            Console.ResetColor();
        }

        static void SuccessMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"[{DateTime.Now.ToString()}] " + message);
            Console.ResetColor();
        }

        static void InfoMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[{DateTime.Now.ToString()}] " + message);
            Console.ResetColor();
        }

        class AccountInfo
        {
            // Группа - default
            // Инвентарь - {}
            public string name;
            public DateTime lastLogin;
            public int balance;
            public string discord;
            public byte deleted;

            public string status
            {
                get
                {
                    if (deleted == 2)
                        return "Удален";
                    if (deleted == 1)
                        return "Не полностью удален";
                    return "Не удален";
                }
            }
        }
    }
}

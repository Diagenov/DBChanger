﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Data;
using System.IO;
using System.Data.Common;
using Mono.Data.Sqlite;
using Newtonsoft.Json;

namespace DBChanger
{
    class Program
    {
        static SqliteConnection tShockDb;
        static SqliteConnection minigamesDb;
        static DateTime LastTitleUpdate = DateTime.Now;

        static void Main(string[] args)
        {
            //MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            //ClearEmptyAccounts().ConfigureAwait(false).GetAwaiter().GetResult();
            //TwinkiesSearch().ConfigureAwait(false).GetAwaiter().GetResult();
            //RemoveTwinks().ConfigureAwait(false).GetAwaiter().GetResult();
            //CheckExists().ConfigureAwait(false).GetAwaiter().GetResult();
            //MoneysAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            Community().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        #region Удаление заброшенных аккаунтов
        static async Task Community()
        {
            InfoMessage("[DBChanger] Подключение к tshock.sqlite . . .");
            tShockDb = await GetConnection(Path.Combine("tshock.sqlite"));
            if (tShockDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Получение списка игроков . . .");
            var list = await GetCommunity();
            if (list == null || list.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }
            File.WriteAllText(Path.Combine("community.txt"), string.Join("\n\n\n", list.OrderByDescending(x => x.Twinks.Count > 0 ? Math.Max(x.Count, x.Twinks.Max(y => y.Count)) : x.Count).Select(x => x.Text)));
            SuccessMessage("[DBChanger] Готово!");
            await Task.Delay(-1);
        }

        static async Task<List<CommunityInfo>> GetCommunity()
        {
            var list = new List<CommunityInfo>();
            using (var cmd = tShockDb.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `Users`;";
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var name = r.IsDBNull(1) ? "" : r.GetString(1);
                            var uuid = r.IsDBNull(3) ? "" : r.GetString(3);
                            var group = r.IsDBNull(4) ? "" : r.GetString(4);

                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(group))
                            {
                                continue;
                            }
                            var registered = r.IsDBNull(5) ? default : DateTime.Parse(r.GetString(5)).ToLocalTime();
                            var lastLogin = r.IsDBNull(6) ? default : DateTime.Parse(r.GetString(6)).ToLocalTime();

                            if (registered == default || lastLogin == default)
                            {
                                continue;
                            }
                            var count = (lastLogin - registered).Ticks;
                            var lifetime = new DateTime(count);
                            var text = "";

                            if (lifetime.Year - 1 > 0)
                            {
                                text += $"{lifetime.Year - 1} years";
                            }
                            if (lifetime.Month - 1 > 0)
                            {
                                text += $" {lifetime.Month - 1} months";
                            }
                            if (lifetime.Day - 1 > 0)
                            {
                                text += $" {lifetime.Day - 1} days";
                            }
                            if (text == "")
                            {
                                continue;
                            }
                            var commi = new CommunityInfo
                            {
                                Name = name,
                                UUID = uuid,
                                Group = group,
                                Registered = registered.ToString("dd.MM.yyyy"),
                                Count = count,
                                LifeTime = text,
                            };
                            var twink = list.Find(x => x.UUID == uuid);
                            if (twink != default)
                            {
                                twink.Twinks.Add(commi);
                                continue;
                            }
                            list.Add(commi);
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
        #endregion

        #region Получение монет
        static async Task MoneysAsync()
        {
            InfoMessage("[DBChanger] Подключение к Minigames.sqlite . . .");
            minigamesDb = await GetConnection(Path.Combine("Minigames.sqlite"));
            if (minigamesDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Получение списка игроков . . .");
            var list = await GetAllPlayers();
            if (list == null || list.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }
            list = list.OrderBy(x => x.Item2).ToList();

            var distribution = new List<int>();
            var all = 0L;

            foreach (var x in list.ToArray())
            {
                if (x.Item2 < -5000 || x.Item2 > 240000)
                {
                    list.Remove(x);
                    continue;
                }

                all += x.Item2;
                if (x.Item2 < 0)
                    continue;
                
                int index = (int)Math.Floor(x.Item2 / 5000d);
                for (; distribution.Count < index + 1;)
                {
                    distribution.Add(0);
                }
                distribution[index]++;
            }

            var text = new List<string>();
            var sum = distribution.Sum() * 1d;

            int i = 0;
            foreach (var x in distribution)
            {
                var t = $"{i * 5000} to {++i * 5000}";
                text.Add($"{t}{new string(' ', 18 - t.Length)}*  {string.Format("{0:f4}", 100d * (x / sum))}%");
            }
            i = 1;
            list.Reverse();

            string results = 
                " --- --- --- ТОП ПО КОЛИЧЕСТВУ МОНЕТ НА РУКАХ --- --- ---" + '\n' +
                '\n' +
                '\n' +
                '\n' +
                " --- Процент игроков с разным кол-вом монет ---" + '\n' +
                string.Join("\n", text) + '\n' +
                '\n' +
                '\n' +
                '\n' +
                " --- Список ---" + '\n' +
                string.Join("\n", list.Select(x => $" {i}) {x.Item1}{new string(' ', 27 - x.Item1.Length - $"{i++}".Length)}*  {x.Item2}")) + '\n' +
                '\n' +
                '\n' +
                '\n' +
                $"Дата: {DateTime.Now}, Автор программы: Диагенов Михаил";


            File.WriteAllText($"{Environment.CurrentDirectory}\\DBChanger results.txt", results);
            SuccessMessage("[DBChanger] Готово!");
            await Task.Delay(-1);
        }

        static async Task<List<Tuple<string, int>>> GetAllPlayers()
        {
            var list = new List<Tuple<string, int>>();
            using (var cmd = minigamesDb.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `Players`;";
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            list.Add(Tuple.Create(r.GetString(0), r.GetInt32(1)));
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
        #endregion

        #region Проверка на существование 
        static async Task CheckExists()
        {
            InfoMessage("[DBChanger] Подключение к tshock.sqlite . . .");
            tShockDb = await GetConnection(Path.Combine("tshock.sqlite"));
            if (tShockDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Подключение к BanSystem.sqlite . . .");
            var banSystem = await GetConnection(Path.Combine("BanSystem.sqlite"));
            if (banSystem == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Получение списка остатков от аккаунтов . . .");
            var list = await GetList(banSystem);
            if (list == null || list.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Удаление найденных остатков . . .");
            list.ForEach(async x => await Delete(banSystem, x));

            File.WriteAllText(Path.Combine("DBChanger results.txt"), string.Join("\n", list));
            SuccessMessage("[DBChanger] Готово!");
            await Task.Delay(-1);
        }

        static async Task<List<string>> GetList(SqliteConnection database)
        {
            var list = new List<string>();
            try
            {
                using (var cmd = database.CreateCommand())
                {
                    cmd.CommandText = $"SELECT * FROM `Accounts`;";
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                            list.Add(r.GetString(0));
                    }
                }
                using (var cmd = tShockDb.CreateCommand())
                {
                    cmd.CommandText = $"SELECT * FROM `Users`;";
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                            list.Remove(r.GetString(1));
                        return list;
                    }
                }
            }
            catch (DbException ex)
            {
                ErrorMessage($"[Database] [BanSystem & tshock] Error: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                ErrorMessage($"[Database] [BanSystem & tshock] Error: {ex.Message}");
            }
            catch (InvalidCastException ex)
            {
                ErrorMessage($"[Database] [BanSystem & tshock] Error: {ex.Message}");
            }
            return null;
        }

        static async Task Delete(SqliteConnection database, string username)
        {
            using (var cmd = database.CreateCommand())
            {
                cmd.CommandText = $"DELETE FROM `Accounts` WHERE `Username`='{username.Replace("'", "''")}';";
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [BanSystem] Error: {ex.Message}");
                }
            }
        }
        #endregion

        #region Чистка дефектных аккаунтов
        static async Task ClearEmptyAccounts()
        {
            InfoMessage("[DBChanger] Подключение к tshock.sqlite . . .");
            tShockDb = await GetConnection(Path.Combine("tshock.sqlite"));
            if (tShockDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Подключение к Minigames.sqlite . . .");
            minigamesDb = await GetConnection(Path.Combine("Minigames.sqlite"));
            if (minigamesDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            LoadBadwords();
            InfoMessage("[DBChanger] Получение списка пустых игроков . . .");
            var list = await GetEmptiesList();
            if (list == null || list.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            int count = 0;
            InfoMessage("[DBChanger] Удаление отобранных игроков . . .");
            foreach (var x in list)
            {
                if ((DateTime.Now - LastTitleUpdate).TotalMilliseconds > 100)
                {
                    Console.Title = $"Обработано результатов: {count}/{list.Count}";
                    LastTitleUpdate = DateTime.Now;
                }
                if (await Delete(tShockDb, "Users", x.name))
                    x.deleted++;
                if (await Delete(minigamesDb, "Players", x.name))
                    x.deleted++;
                count++;
            }

            string results = 
                " --- --- --- РЕЗУЛЬТАТЫ ЧИСТКИ ПУСТЫХ АККАУНТОВ --- --- ---" + '\n' +
                '\n' +
                '\n' +
                '\n' +
                " --- Условия удаления ---" + '\n' +
                "Группа: default" + '\n' +
                "Никнейм: некорректный" + '\n' +
                "Активность: менее 10 дней с момента регистрации" + '\n' +
                "Последний логин: больше 90 дней назад или никогда" + '\n' +
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
                "  *  Name - Last Login - Registered - Reson" + '\n' +
                string.Join("\n", list.Select(x => $"  *  {x.name} - {x.lastLogin} - {x.registered} - {x.reason}")) + '\n' +
                '\n' +
                '\n' +
                '\n' +
                $"Дата: {DateTime.Now.ToString()}, Автор программы: Диагенов Михаил";

            File.WriteAllText($"{Environment.CurrentDirectory}\\DBChanger results.txt", results);
            SuccessMessage("[DBChanger] Готово!");
            await Task.Delay(-1);
        }

        static async Task<List<EmptyAccount>> GetEmptiesList()
        {
            var list = new List<EmptyAccount>();
            using (var cmd = tShockDb.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `Users` WHERE `Usergroup`='default';";
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                        {
                            var username = r.GetString(1);
                            var uuid = r.IsDBNull(3) ? "" : r.GetString(3);
                            var ips = r.IsDBNull(7) ? "" : r.GetString(7);

                            var lastLogin = r.IsDBNull(6) ? 
                                new DateTime() : 
                                DateTime.Parse(r.GetString(6)).ToLocalTime();

                            var registered = r.IsDBNull(5) ?
                                new DateTime() :
                                DateTime.Parse(r.GetString(5)).ToLocalTime();

                            if (string.IsNullOrWhiteSpace(uuid) || string.IsNullOrWhiteSpace(ips) 
                             || new DateTime() == lastLogin || new DateTime() == registered)
                            {
                                list.Add(new EmptyAccount
                                {
                                    name = username,
                                    lastLogin = lastLogin,
                                    registered = registered,
                                    reason = "пустой аккаунт"
                                });
                            }
                            else if ((lastLogin - registered).TotalDays < 10 && (DateTime.Now - lastLogin).TotalDays > 90)
                            {
                                list.Add(new EmptyAccount
                                {
                                    name = username,
                                    lastLogin = lastLogin,
                                    registered = registered,
                                    reason = "аккаунт однодневка"
                                });
                            }
                            else if (!CheckNickname(username, out string reason))
                            {
                                list.Add(new EmptyAccount
                                {
                                    name = username,
                                    lastLogin = lastLogin,
                                    registered = registered,
                                    reason = reason
                                });
                            }
                        }
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                }
                catch (InvalidCastException ex)
                {
                    ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                }
            }
            return list;
        }

        static bool CheckNickname(string username, out string reason)
        {
            var access = "абвгдеёжзийклмнопрстуфхцчшщъыьэюяАБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890~`!@#$%^&*()_+\"№;%:?-=\\/|[]{}'<>.,";
            if (username.Any(x => !access.Contains(x)))
            {
                reason = "неклавиатурные символы в нике";
                return false;
            }
            if (username.Length > 20)
            {
                reason = "длинный ник";
                return false;
            }
            if (Regex.IsMatch(username, @"\[[icnag].+", RegexOptions.IgnoreCase)) // @"\[[icnag].+\]"
            {
                reason = "неклавиатурные символы в нике";
                return false;
            }
            if (HasBadword(username))
            {
                reason = "плохие слова в нике";
                return false;
            }
            reason = "";
            return true;
        }

        public static Dictionary<char, List<string>> Badwords = new Dictionary<char, List<string>>();
        public static Dictionary<char, char> SimilarChars = new Dictionary<char, char>
        {
            { 'x', 'х' },
            { 'y', 'у' },
            { 'o', 'о' },
            { 'e', 'е' },
            { 'c', 'с' },
            { 'k', 'к' },
            { 'a', 'а' },
            { 'u', 'и' },
        };

        public static void LoadBadwords()
        {
            var path = Path.Combine("badwords.txt");
            if (!File.Exists(path))
                return;

            Badwords.Clear();
            foreach (var word in File.ReadAllLines(path))
            {
                if (!Badwords.ContainsKey(word[0]))
                    Badwords.Add(word[0], new List<string>());
                Badwords[word[0]].Add(word);
            }
        }

        public static bool HasBadword(string text)
        {
            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length < 3)
                    continue;
                var lowerInvariant = words[i].ToLowerInvariant();

                var translit = ' ' + lowerInvariant;
                foreach (var x in SimilarChars)
                {
                    if (translit.Contains(x.Key))
                        translit = translit.Replace(x.Key, x.Value);
                }
                lowerInvariant += translit;

                if (Badwords.TryGetValue(lowerInvariant[0], out List<string> list) && list.Any(x => lowerInvariant.StartsWith(x)))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Твинки на удаление
        static async Task RemoveTwinks()
        {
            InfoMessage("[DBChanger] Подключение к twinks.sqlite . . .");
            var twinksDb = await GetConnection(Path.Combine("twinks.sqlite"));
            if (twinksDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Подключение к BanSystem.sqlite . . .");
            var banSystem = await GetConnection(Path.Combine("BanSystem.sqlite"));
            if (banSystem == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Подключение к Minigames.sqlite . . .");
            minigamesDb = await GetConnection(Path.Combine("Minigames.sqlite"));
            if (minigamesDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Подключение к tshock.sqlite . . .");
            tShockDb = await GetConnection(Path.Combine("tshock.sqlite"));
            if (tShockDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Получение списка целей . . .");
            var targets = await GetTargets(twinksDb);
            if (targets == null || targets.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Получение списка пожеланий . . .");
            var wishes = await GetWishes(banSystem);
            if (wishes == null || wishes.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Сортировка списка целей по баллам . . .");
            if (!await CheckList(targets, wishes))
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            var deleted = new List<TwinkAccount>();
            InfoMessage("[DBChanger] Удаление отобранных игроков . . .");
            foreach (var i in targets)
            {
                foreach (var x in i.twinks.Skip(3))
                {
                    await Delete(tShockDb, "Users", x.name);
                    await Delete(minigamesDb, "Players", x.name);
                    deleted.Add(x);
                }
            }

            string results = 
                " --- --- --- РЕЗУЛЬТАТЫ ЧИСТКИ ТВИНКОВ --- --- ---" + '\n' +
                '\n' +            
                '\n' +           
                '\n' +            
                " --- Критерии к не удалению твинка (по очкам) ---" + '\n' +
                "  +25     любовь создателя" + '\n' +
                "   +5     особая группу" + '\n' +            
                "+1 +2 +3  актуальность логина" + '\n' +
                "+1 +2 +3  величина баланса" + '\n' +
                "+1 +2 +3  \"длина\" костюма" + '\n' +
                "+1 +2 +3  \"длина\" значков" + '\n' +
                '\n' +           
                '\n' +             
                '\n' +          
                " --- Общие сведения ---" + '\n' +            
                $"Людей с 4 и более аккаунтами: {targets.Count}" + '\n' +         
                $"Всего аккаунтов: {targets.Sum(x => x.twinks.Count)}" + '\n' +   
                $"Удалено аккаунтов: {deleted.Count}" + '\n' +
                '\n' +           
                '\n' +          
                '\n' +
                " --- Список удалённых аккаунтов ---" + '\n' +
                string.Join("\n", deleted.Select(x => $"{x.name}  {new string(' ', 20 - x.name.Length)}->    {x.group} --- {x.lastLogin} --- {x.balance} coins")) + '\n' +
                '\n' +
                '\n' +
                '\n' +
                $"Дата: {DateTime.Now}, Автор программы: Диагенов Михаил";


            File.WriteAllText($"{Environment.CurrentDirectory}\\DBChanger results.txt", results);
            SuccessMessage("[DBChanger] Готово!");
            await Task.Delay(-1);
        }

        static async Task<List<TwinkAccount>> GetTargets(SqliteConnection twinksDb)
        {
            var list = new List<TwinkAccount>();
            using (var cmd = twinksDb.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `Twinks` WHERE `Count`>3;";
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var info = r.GetString(1)
                                .Split('\n');
                            var twinks = r.GetString(2)
                                .Split(new string[] { "\n\n" }, StringSplitOptions.None)
                                .Select(x => x.Split('\n'));

                            list.Add(new TwinkAccount
                            {
                                twinks = twinks.Select(x => new TwinkAccount
                                {
                                    name = x[0],
                                    group = x[1],
                                    lastLogin = DateTime.Parse(x[2]).ToLocalTime(),
                                }).ToList()
                            });
                            list.Last().twinks.Add(new TwinkAccount
                            {
                                name = info[0],
                                group = info[1],
                                lastLogin = DateTime.Parse(info[2]).ToLocalTime()
                            });
                        }
                    }
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [twinks] Error: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    ErrorMessage($"[Database] [twinks] Error: {ex.Message}");
                }
                catch (InvalidCastException ex)
                {
                    ErrorMessage($"[Database] [twinks] Error: {ex.Message}");
                }
            }
            return list;
        }

        static async Task<List<List<string>>> GetWishes(SqliteConnection database)
        {
            var list = new List<List<string>>();
            using (var cmd = database.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `Some`";
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var wish = new List<string>
                            {
                                r.GetString(0)
                            };
                            wish.AddRange(r.GetString(1).Split('\n'));
                            list.Add(wish);
                        }
                    }
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [BanSystem] Error: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    ErrorMessage($"[Database] [BanSystem] Error: {ex.Message}");
                }
                catch (InvalidCastException ex)
                {
                    ErrorMessage($"[Database] [BanSystem] Error: {ex.Message}");
                }
            }
            return list;
        }

        static async Task<bool> CheckList(List<TwinkAccount> list, List<List<string>> wishes)
        {
            using (var cmd = minigamesDb.CreateCommand())
            {
                try
                {
                    foreach (var i in list)
                    {
                        var wish = wishes.FindAll(x => i.twinks.Any(y => x.First() == y.name));
                        if (wish.Count > 0)
                        {
                            foreach (var x in i.twinks)
                            {
                                if (wish.Any(y => y.Contains(x.name)))
                                    x.points += 25;
                            }
                        }

                        var order = i.twinks.OrderBy(x => (DateTime.Now - x.lastLogin).TotalDays).ToArray();
                        for (int x = 0; x < Math.Min(3, order.Length); x++)
                        {
                            order[x].points += 3 - x;
                        }

                        foreach (var x in i.twinks)
                        {
                            if (x.group != "default")
                                x.points += 5;

                            cmd.CommandText =
                                $"SELECT * FROM `Players` WHERE `Username`='{x.name.Replace("'", "''")}';";

                            using (var r = await cmd.ExecuteReaderAsync())
                            {
                                if (await r.ReadAsync())
                                {
                                    x.balance = r.GetInt32(1);
                                    x.invLength = r.IsDBNull(2) ? 0 : r.GetString(2).Length;
                                    x.pinLength = r.IsDBNull(6) ? 0 : r.GetString(6).Length;
                                }
                            }
                        }

                        order = i.twinks.OrderByDescending(x => x.balance).ToArray();
                        for (int x = 0; x < Math.Min(3, order.Length); x++)
                        {
                            order[x].points += 3 - x;
                        }

                        order = i.twinks.OrderByDescending(x => x.invLength).ToArray();
                        for (int x = 0; x < Math.Min(3, order.Length); x++)
                        {
                            order[x].points += 3 - x;
                        }

                        order = i.twinks.OrderByDescending(x => x.pinLength).ToArray();
                        for (int x = 0; x < Math.Min(3, order.Length); x++)
                        {
                            order[x].points += 3 - x;
                        }
                        i.twinks = i.twinks.OrderByDescending(x => x.points).ToList();
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
        #endregion

        #region Получение твинков
        static async Task TwinkiesSearch()
        {
            InfoMessage("[DBChanger] Подключение к tshock.sqlite . . .");
            tShockDb = await GetConnection(Path.Combine("tshock.sqlite"));
            if (tShockDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Получение списка игроков . . .");
            var all = await GetAll();
            if (all == null || all.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Подключение к BanSystem.sqlite . . .");
            var banSystem = await GetConnection(Path.Combine("BanSystem.sqlite"));
            if (banSystem == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Составление списка твинков . . .");
            var list = new List<TwinkAccount>();
            while (all.Count > 1)
            {
                if ((DateTime.Now - LastTitleUpdate).TotalMilliseconds > 100)
                {
                    Console.Title = $"Осталось проверить: {all.Count}";
                    LastTitleUpdate = DateTime.Now;
                }

                var user = all[0];
                var names = await GetUsernamesByUUID(banSystem, user.uuid);

                if (names.Count > 1)
                {
                    user.twinks.AddRange(all.Skip(1).Where(x => names.Contains(x.name)));
                    list.Add(user);
                }
                all.RemoveAll(x => names.Contains(x.name));
            }
            if (list.Count == 0)
            {
                InfoMessage("[DBChanger] Список пуст.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Подключение к twinks.sqlite . . .");
            var db = await GetTwinksDatabase();
            if (db == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Составление базы данных о твинках . . .");
            int success = 0;
            int errors = 0;
            foreach (var x in list)
            {
                if ((DateTime.Now - LastTitleUpdate).TotalMilliseconds > 100)
                {
                    Console.Title = $"Обработано результатов: {success + errors}/{list.Count}";
                    LastTitleUpdate = DateTime.Now;
                }
                if (await SaveTwink(db, x))
                    success++;
                else
                    errors++;
            }
            SuccessMessage($"[DBChanger] Успешных операций: {success}/{list.Count}.");
            SuccessMessage("[DBChanger] Готово!");
            await Task.Delay(-1);
        }

        static async Task<List<TwinkAccount>> GetAll()
        {
            var list = new List<TwinkAccount>();
            using (var cmd = tShockDb.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `Users`;";
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            var lastLogin = r.IsDBNull(6) ? 
                                new DateTime() : 
                                DateTime.Parse(r.GetString(6)).ToLocalTime();
                            list.Add(new TwinkAccount
                            {
                                name = r.GetString(1),
                                uuid = r.GetString(3),
                                group = r.GetString(4),
                                lastLogin = lastLogin,
                                twinks = new List<TwinkAccount>()
                            });
                            if ((DateTime.Now - LastTitleUpdate).TotalMilliseconds > 100)
                            {
                                Console.Title = $"Загружено аккаунтов: {list.Count}";
                                LastTitleUpdate = DateTime.Now;
                            }
                        }
                        return list;
                    }
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                }
                catch (InvalidCastException ex)
                {
                    ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                }
            }
            return list;
        }

        static async Task<List<Tuple<string, List<string>>>> GetByUUID(SqliteConnection database, params string[] UUIDs)
        {
            var list = new List<Tuple<string, List<string>>>();
            using (var cmd = database.CreateCommand())
            {
                foreach (var uuid in UUIDs)
                {
                    if (string.IsNullOrWhiteSpace(uuid) || uuid.Length < 128)
                        continue;

                    cmd.CommandText = $"SELECT * FROM `Accounts` WHERE INSTR(`UUIDs`, '{uuid}') > 0;";
                    try
                    {
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            while (await r.ReadAsync())
                            {
                                var username = r.GetString(0);
                                if (list.Any(i => i.Item1 == username))
                                {
                                    continue;
                                }
                                list.Add(Tuple.Create(username,
                                    JsonConvert.DeserializeObject<string[]>(r.GetString(2)).ToList()));
                            }
                            return list;
                        }
                    }
                    catch (DbException ex)
                    {
                        ErrorMessage($"[Database] [BanSystem] Error: {ex.Message}");
                    }
                    catch (ArgumentException ex)
                    {
                        ErrorMessage($"[Database] [BanSystem] Error: {ex.Message}");
                    }
                    catch (InvalidCastException ex)
                    {
                        ErrorMessage($"[Database] [BanSystem] Error: {ex.Message}");
                    }
                }
            }
            return list;
        }

        static async Task<List<string>> GetUsernamesByUUID(SqliteConnection database, string UUID)
        {
            var list = await GetByUUID(database, UUID);
            var uuids = new List<string>();
            var usernames = new List<string>();

            foreach (var i in list)
            {
                if (i.Item2.Count < 2)
                {
                    continue;
                }
                i.Item2.Remove(UUID);
                foreach (var uuid in i.Item2)
                {
                    if (!uuids.Contains(uuid))
                        uuids.Add(uuid);
                }
            }

            if (uuids.Count > 0)
            {
                foreach (var i in await GetByUUID(database, uuids.ToArray()))
                    foreach (var uuid in i.Item2)
                    {
                        if (!uuids.Contains(uuid))
                            uuids.Add(uuid);
                    }
            }
            if (!uuids.Contains(UUID))
            {
                uuids.Add(UUID);
            }

            using (var cmd = tShockDb.CreateCommand())
            {
                foreach (var uuid in uuids)
                {
                    cmd.CommandText = $"SELECT * FROM `Users` WHERE `UUID`='{uuid}';";
                    try
                    {
                        using (var r = await cmd.ExecuteReaderAsync())
                        {
                            while (await r.ReadAsync())
                            {
                                var username = r.GetString(1);
                                if (!usernames.Contains(username))
                                    usernames.Add(username);
                            }
                        }
                    }
                    catch (DbException ex)
                    {
                        ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                    }
                    catch (ArgumentException ex)
                    {
                        ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                    }
                    catch (InvalidCastException ex)
                    {
                        ErrorMessage($"[Database] [tshock] Error: {ex.Message}");
                    }
                }
            }
            return usernames;
        }

        static async Task<SqliteConnection> GetTwinksDatabase()
        {
            var path = Path.Combine("twinks.sqlite");
            if (!File.Exists(path))
            {
                SqliteConnection.CreateFile(path);
            }

            InfoMessage("[DBChanger] Подключение к twinks.sqlite . . .");
            var db = await GetConnection(path);
            if (db == null)
            {
                return null;
            }

            InfoMessage("[DBChanger] Создание таблицы твинков . . .");
            using (var cmd = new SqliteCommand { Connection = db })
            {
                try
                {
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS `Twinks` (`Count` INTEGER NOT NULL, `Author` VARCHAR(100) NOT NULL, `Creatures` VARCHAR(10000) NOT NULL);";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [twinks] Error: {ex.Message}");
                    return null;
                }
            }
            return db;
        }

        static async Task<bool> SaveTwink(SqliteConnection db, TwinkAccount twink)
        {
            var count = twink.twinks.Count + 1;
            var author = twink.ToString();
            var creatures = string.Join("\n\n", twink.twinks.Select(x => x.ToString()));

            if (author.Length > 100 || creatures.Length > 10000)
            {
                ErrorMessage($"[Database] {twink.name}: превышение лимита на символы.");
                return false;
            }

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = $"INSERT INTO `Twinks` (`Count`, `Author`, `Creatures`) VALUES ({count}, '{author}', '{creatures}');";
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [twinks] Error: {ex.Message}");
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region Сброс значков за паркуры
        static async Task ParkourClearAsync()
        {
            InfoMessage("[DBChanger] Подключение к Minigames.sqlite . . .");
            minigamesDb = await GetConnection(Path.Combine("tshock", "Minigames", "Minigames.sqlite"));
            if (minigamesDb == null)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Получение списка игроков . . .");
            var list = await GetParkourList();
            if (list == null || list.Count == 0)
            {
                InfoMessage("[DBChanger] Полезный процесс завершен.");
                await Task.Delay(-1);
            }

            InfoMessage("[DBChanger] Проверка значков за паркуры у отобранных игроков . . .");
            foreach (var x in list.ToList())
            {
                x.result = await CheckParkourPins(x);
                if (x.result.Count == 0)
                    list.Remove(x);
            }

            string results = " --- --- --- РЕЗУЛЬТАТЫ ПРОВЕРКИ ЗНАЧКОВ ЗА ПАРКУРЫ --- --- ---" + '\n' +
                             '\n' +
                             '\n' +
                             '\n' +
                             " --- Всего забрали значков ---" + '\n' +
                             $"За 1 ветку (6): {list.Count(x => x.result.Contains(6))}" + '\n' +
                             $"За 2 ветку (7): {list.Count(x => x.result.Contains(7))}" + '\n' +
                             $"За 3 ветку (8): {list.Count(x => x.result.Contains(8))}" + '\n' +
                             $"За 4 ветку (25): {list.Count(x => x.result.Contains(25))}" + '\n' +
                             '\n' +
                             '\n' +
                             '\n' +
                             " --- Список игроков, у которых забрали значки ---" + '\n' +
                             "  *  Name - Removed Pins (IDs)" + '\n' +
                             string.Join("\n", list.Select(x => $"  *  {x.name} - {string.Join(", ", x.result)}")) + '\n' +
                             '\n' +
                             '\n' +
                             '\n' +
                             $"Дата: {DateTime.Now.ToString()}, Автор программы: Диагенов Михаил";


            File.WriteAllText($"{Environment.CurrentDirectory}\\DBChanger results.txt", results);
            SuccessMessage("[DBChanger] Готово!");
            await Task.Delay(-1);
        }

        static async Task<List<ParkourAccount>> GetParkourList()
        {
            var list = new List<ParkourAccount>();
            using (var cmd = minigamesDb.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM `Players`;";
                try
                {
                    using (var r = await cmd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            if (r.IsDBNull(6) || r.GetString(6) == "[]")
                                continue;

                            list.Add(new ParkourAccount(
                                r.GetString(0),
                                r.GetString(4),
                                r.GetString(6)));
                        }
                        return list;
                    }
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.ToString()}");
                }
                catch (ArgumentException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.ToString()}");
                }
                catch (InvalidCastException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.ToString()}");
                }
            }
            return null;
        }

        static async Task<List<int>> CheckParkourPins(ParkourAccount user)
        {
            var removedPins = new List<int>();

            if (user.parkour[0] <= 25 && user.pins.Remove(6))
                removedPins.Add(6);

            if (user.parkour[0] <= 20 && user.pins.Remove(7))
                removedPins.Add(7);

            if (user.parkour[0] <= 15 && user.pins.Remove(8))
                removedPins.Add(8);

            if (user.parkour[0] <= 17 && user.pins.Remove(25))
                removedPins.Add(25);

            if (removedPins.Count == 0)
                return removedPins;

            string pins = $"[{string.Join(",", user.pins)}]";

            using (var cmd = minigamesDb.CreateCommand())
            {
                cmd.CommandText = $"UPDATE `Players` SET `Pins`='{pins}' WHERE `Username`='{user.name.Replace("'", "''")}';";
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                    return removedPins;
                }
                catch (DbException ex)
                {
                    ErrorMessage($"[Database] [Minigames] Error: {ex.Message}");
                    return new List<int>();
                }
            }
        }
        #endregion

        #region Удаление заброшенных аккаунтов
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
        #endregion

        #region Console messages
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
        #endregion

        #region Классы с полями
        class CommunityInfo
        {
            public string Name;
            public long Count;
            public string LifeTime;
            public string Registered;
            public string Group;
            public string UUID;
            public List<CommunityInfo> Twinks = new List<CommunityInfo>();

            public string Text
            {
                get
                {
                    var text = $"{Name}\n   {Group}\n   {Registered}\n   {LifeTime}";
                    if (Twinks.Count > 0)
                    {
                        return text + $"\n   {string.Join("   ", Twinks.Select(x => x.Text))}";
                    }
                    return $"{text}\n";
                }
            }
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
    
        class ParkourAccount
        {
            public string name;
            public int[] parkour;
            public List<int> pins;
            public List<int> result;

            public ParkourAccount(string name, string parkour, string pins)
            {
                this.name = name;
                result = new List<int>();

                this.parkour = parkour
                    .Substring(1, parkour.Length - 2)
                    .Split(',').Select(i => int.Parse(i))
                    .ToArray();

                this.pins = pins
                    .Substring(1, pins.Length - 2)
                    .Split(',').Select(i => int.Parse(i))
                    .ToList();
            }
        }
    
        class TwinkAccount
        {
            public string name;
            public string uuid;
            public string group;
            public DateTime lastLogin;
            public List<TwinkAccount> twinks;
            
            public int points;
            public int balance;
            public int invLength;
            public int pinLength;

            public override string ToString()
            {
                return $"{name.Replace("'", "''")}\n{group}\n{lastLogin.ToString("s")}";
            }
        }
    
        class EmptyAccount
        {
            public string name;
            public DateTime lastLogin;
            public DateTime registered;
            public byte deleted;
            public string reason;

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
        
        class AccountSkin
        {
            public string Username;
            public int AccountID;
            public int Skin;
            //public int Hair; = 55
            public int HairColor;
            public int PantsColor;
            public int ShirtColor;
            public int UnderShirtColor;
            public int ShoeColor;
            public int SkinColor;
            public int EyeColor;

            public AccountSkin(DbDataReader r)
            {
                AccountID = r.GetInt32(0);
                Skin = r.GetInt32(9);
                //Hair = r.GetInt32(10);
                HairColor = r.GetInt32(12);
                PantsColor = r.GetInt32(13);
                ShirtColor = r.GetInt32(14);
                UnderShirtColor = r.GetInt32(15);
                ShoeColor = r.GetInt32(16);
                SkinColor = r.GetInt32(18);
                EyeColor = r.GetInt32(19);
            }
        }
        #endregion
    }
}

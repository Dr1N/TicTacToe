using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WcfService
{
    [ServiceContract]
    class TicTacService
    {
        static Dictionary<string, GameState> games;
        static Dictionary<string, DateTime> usersLastTime;
        static ReaderWriterLockSlim gameCollectionLocker;
        static ReaderWriterLockSlim gameLocker;
        static TimeSpan timeout;

        static TicTacService()
        {
            games = new Dictionary<string, GameState>();
            usersLastTime = new Dictionary<string, DateTime>();
            gameCollectionLocker = new ReaderWriterLockSlim();
            gameLocker = new ReaderWriterLockSlim();
            timeout = TimeSpan.FromSeconds(5);
        }

        [OperationContract]
        public bool Login(string login)
        {
            try
            {
                //Провека существования логина на сервере

                gameCollectionLocker.EnterReadLock();
                if (games.ContainsKey(login) == true)
                {
                    Console.WriteLine("Отказано во входе: {0}. Причина: Логин уже существует", login);
                    return false;
                }
                gameCollectionLocker.ExitReadLock();

                //Поиск свободной игры

                GameState game = GetWaitingGame();

                if (game == null)   //Свободной игры нет
                {
                    Console.WriteLine("Свободной игры нет. Создаём");

                    //Создание игры

                    game = new GameState();
                    game.firstPlayer = login;
                    game.state = GameState.GAME_STATE.WAIT_SECOND_PLAYER;
                }
                else                //Есть ожидающая игрока игра
                {
                    Console.WriteLine("Есть ожидающая игра. Присоединяемся");

                    game.secondPlayer = login;
                    game.state = GameState.GAME_STATE.FIRST_PLAYER;
                    game.currentPlayer = game.firstPlayer;
                }

                //Добавление игры в коллекцию 

                gameCollectionLocker.EnterWriteLock();
                games[login] = game;
                gameCollectionLocker.ExitWriteLock();

                //Время последнего доступа клиента

                usersLastTime[login] = DateTime.Now;

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            finally
            {
                if (gameCollectionLocker.IsReadLockHeld == true) { gameCollectionLocker.ExitReadLock(); }
                if (gameCollectionLocker.IsWriteLockHeld == true) { gameCollectionLocker.ExitWriteLock(); }
            }
        }

        [OperationContract]
        public GameState GetState(string login)
        {
            try
            {
                usersLastTime[login] = DateTime.Now;
                gameCollectionLocker.EnterReadLock();
                return games[login];
            }
            catch (Exception e)
            {
                Console.WriteLine("Возврат состояния игры клиенту: {0}", login);
                Console.WriteLine(e.Message);
                return null;
            }
            finally
            {
                gameCollectionLocker.ExitReadLock();
                if (gameCollectionLocker.IsWriteLockHeld == true) { gameCollectionLocker.ExitWriteLock(); }
            }
        }

        [OperationContract]
        public void Action(string user, Point coord)
        {
            try
            {
                Console.WriteLine("Ходит {0}. Координаты: {1}", user, coord);

                //Получаем игру

                gameCollectionLocker.EnterReadLock();
                GameState game = games[user];
                gameCollectionLocker.ExitReadLock();

                //Делаем ход

                int index = coord.Y * 3 + coord.X;
                if (game.field[index] == GameState.FIELD_STATE.FREE)
                {
                    game.field[index] = (user == game.firstPlayer) ? GameState.FIELD_STATE.CROSS : GameState.FIELD_STATE.ZERO;
                }
                else
                {
                    Console.WriteLine("Игрок {0} походил в занятую ячейку", user);
                }

                //Проверка окончания игры

                int result = IsEndGame(game);
                if (result == 1)
                {
                    Console.WriteLine("Игра закончена. Победитель: {0}", game.currentPlayer);
                    game.state = GameState.GAME_STATE.END_GAME;
                    return;
                }
                else if (result == 2)
                {
                    Console.WriteLine("Игра закончена. Ничья");
                    game.currentPlayer = null;
                    game.state = GameState.GAME_STATE.END_GAME;
                    return;
                }

                //Передача хода

                if (user == game.firstPlayer)
                {
                    Console.WriteLine("Передача хода второму игроку - {0}", game.secondPlayer);
                    game.currentPlayer = game.secondPlayer;
                    game.state = GameState.GAME_STATE.SECOND_PLAYER;
                }
                else
                {
                    Console.WriteLine("Передача хода первому игроку - {0}", game.firstPlayer);
                    game.currentPlayer = game.firstPlayer;
                    game.state = GameState.GAME_STATE.FIRST_PLAYER;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                if (gameCollectionLocker.IsReadLockHeld == true) { gameCollectionLocker.ExitReadLock(); }
            }
        }

        [OperationContract]
        public void ExitPlayer(string user)
        {
            try
            {
                gameCollectionLocker.EnterWriteLock();
                if (games.ContainsKey(user)) { games.Remove(user); }
                gameCollectionLocker.ExitWriteLock();

                if (usersLastTime.ContainsKey(user)) { usersLastTime.Remove(user); }
                Console.WriteLine("Удалили пользоваеля {0}", user);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                if (gameCollectionLocker.IsWriteLockHeld == true) { gameCollectionLocker.ExitWriteLock(); }
            }
        }

        /*
         * 0 - игра продолжается
         * 1 - есть победитель
         * 2 - ничья
        */
        private int IsEndGame(GameState game)
        {
            GameState.FIELD_STATE[,] field = this.GetField(game);

            if ((field[0, 0] == field[0, 1] && field[0, 0] == field[0, 2] && field[0, 0] != GameState.FIELD_STATE.FREE) ||
                (field[1, 0] == field[1, 1] && field[1, 0] == field[1, 2] && field[1, 0] != GameState.FIELD_STATE.FREE) ||
                (field[2, 0] == field[2, 1] && field[2, 0] == field[2, 2] && field[2, 0] != GameState.FIELD_STATE.FREE) ||
                (field[0, 0] == field[1, 0] && field[0, 0] == field[2, 0] && field[0, 0] != GameState.FIELD_STATE.FREE) ||
                (field[0, 1] == field[1, 1] && field[1, 0] == field[2, 1] && field[0, 1] != GameState.FIELD_STATE.FREE) ||
                (field[0, 2] == field[1, 2] && field[0, 2] == field[2, 2] && field[0, 2] != GameState.FIELD_STATE.FREE) ||
                (field[0, 2] == field[1, 2] && field[0, 2] == field[2, 2] && field[0, 2] != GameState.FIELD_STATE.FREE) ||
                (field[0, 0] == field[1, 1] && field[0, 0] == field[2, 2] && field[0, 0] != GameState.FIELD_STATE.FREE) ||
                (field[0, 2] == field[1, 1] && field[0, 2] == field[2, 0] && field[0, 2] != GameState.FIELD_STATE.FREE))
            {
                return 1;
            }
            else if (game.field[0] != GameState.FIELD_STATE.FREE &&
                    game.field[1] != GameState.FIELD_STATE.FREE &&
                    game.field[2] != GameState.FIELD_STATE.FREE &&
                    game.field[3] != GameState.FIELD_STATE.FREE &&
                    game.field[4] != GameState.FIELD_STATE.FREE &&
                    game.field[5] != GameState.FIELD_STATE.FREE &&
                    game.field[6] != GameState.FIELD_STATE.FREE &&
                    game.field[7] != GameState.FIELD_STATE.FREE &&
                    game.field[8] != GameState.FIELD_STATE.FREE)
            {
                return 2;
            }
            else
            {
                return 0;
            }
        }

        private GameState.FIELD_STATE[,] GetField(GameState game)
        {
            try
            {
                GameState.FIELD_STATE[,] field = new GameState.FIELD_STATE[3, 3];
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        field[i, j] = game.field[i * 3 + j];
                    }
                }
                return field;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        private GameState GetWaitingGame()
        {
            try
            {
                gameCollectionLocker.EnterReadLock();
                KeyValuePair<string, GameState> result = games.First((pair) => pair.Value.state == GameState.GAME_STATE.WAIT_SECOND_PLAYER);
                return result.Value;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            finally
            {
                if (gameCollectionLocker.IsReadLockHeld == true) { gameCollectionLocker.ExitReadLock(); }
            }
        }

        public static void CheckUserLastTime()
        {
            Console.WriteLine("Поток слежения за последним запросом пользователя запущен");
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);

                    //Проверка времени

                    List<string> usersForRemove = new List<string>();

                    foreach (KeyValuePair<string, DateTime> item in usersLastTime)
                    {
                        TimeSpan ts = DateTime.Now - item.Value;

                        Console.WriteLine("Время последнего доступа {0} : {1} сек назад", item.Key, ts.TotalSeconds);

                        //Время ожидания превышено

                        if (ts > timeout)
                        {
                            //Закончить игру

                            Console.WriteLine("Время ожидания для пользователя {0} превышено", item.Key);

                            gameCollectionLocker.EnterReadLock();
                            GameState game = games[item.Key];
                            gameCollectionLocker.ExitReadLock();

                            game.state = GameState.GAME_STATE.END_GAME;
                            game.currentPlayer = (item.Key == game.firstPlayer) ? game.secondPlayer : game.firstPlayer;

                            gameCollectionLocker.EnterWriteLock();
                            games.Remove(item.Key);
                            gameCollectionLocker.ExitWriteLock();

                            usersForRemove.Add(item.Key);

                            Console.WriteLine("Пользователь {0} удалён из игры", item.Key);
                        }
                    }

                    //Удаляем пользователей из коллекции времени последнего доступа

                    foreach (string user in usersForRemove)
                    {
                        usersLastTime.Remove(user);
                        Console.WriteLine("Время пользователя {0} удалёно", user);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Во время обновления последнего запроса что-то пошло не так");
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
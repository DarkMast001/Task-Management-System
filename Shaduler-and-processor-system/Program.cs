using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Task_generator_system;

namespace Shaduler_and_processor_system
{
    internal class Program
    {
        static ConcurrentQueue<Socket> userQueue = new ConcurrentQueue<Socket>();
        static object queueLock = new object();
        static int countOfConnectedUsers = 0;
        static Shaduler? shaduler;

        private static void DisconnectUser(Socket listener, int countOfCompletedTasks, int executionTime)
        {
            string str = $"{countOfCompletedTasks} задач было выполнено за {executionTime}\n";
            Console.WriteLine(str);
            listener.Send(Encoding.UTF8.GetBytes(str));
            listener.Send(Encoding.UTF8.GetBytes("200"));
            listener.Shutdown(SocketShutdown.Both);
            listener.Close();
            if (shaduler != null)
                shaduler.UserSocket = null;
        }

        static void userProcessing(object? listenerObj)
        {
            if (listenerObj == null || shaduler == null)
                return;

            Socket listener = (Socket)listenerObj;

            int position;
            lock (queueLock)
            {
                userQueue.Enqueue(listener);
                position = userQueue.Count;
            }
            listener.Send(Encoding.UTF8.GetBytes($"Пользователь добавлен в очередь. Позиция: {position}\n"));

            while (true)
            {
                if (shaduler.UserSocket == null)
                {
                    Socket? nextUser = null;
                    if (userQueue.TryDequeue(out nextUser))
                    {
                        nextUser.Send(Encoding.UTF8.GetBytes("Пользователь извлечен из очереди для обработки\n"));

                        if (nextUser != null)
                        {
                            try
                            {
                                shaduler.UserSocket = nextUser;
                                shaduler.NotifyingUserOfCompletionTasks += DisconnectUser;
                                byte[] buffer = new byte[256];
                                int size = 0;
                                StringBuilder data = new StringBuilder();

                                do
                                {
                                    size = nextUser.Receive(buffer);
                                    data.Append(Encoding.UTF8.GetString(buffer, 0, size));
                                }
                                while (nextUser.Available > 0);

                                TaskGenerator? taskGenerator = TaskGenerator.TryCreate(data.ToString());
                                if (taskGenerator == null)
                                {
                                    nextUser.Send(Encoding.UTF8.GetBytes("BAD DATA"));
                                }
                                else
                                {
                                    List<PriorityTask> priorityTasks = taskGenerator.GetTasks();
                                    foreach (PriorityTask priorityTask in priorityTasks)
                                    {
                                        shaduler.Enqueue(priorityTask);
                                    }
                                    priorityTasks.Clear();
                                }

                                Console.WriteLine(data);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Ошибка при обработке пользователя: {ex.Message}");
                                shaduler.UserSocket = null;
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        static void Main(string[] args)
        {
            Queue<TaskGenerator> queueOfTaskGenerators = new Queue<TaskGenerator>();

            #region SHADULER INIT
            uint workerCount = 0;

            while (workerCount == 0)
            {
                Console.Write("Введите количество процессоров: ");
                try
                {
                    workerCount = Convert.ToUInt32(Console.ReadLine());
                }
                catch (OverflowException ex)
                {
                    Console.WriteLine(ex.Message);
                    workerCount = 0;
                }
                catch (FormatException ex)
                {
                    Console.WriteLine(ex.Message);
                    workerCount = 0;
                }
            }

            shaduler = new Shaduler(workerCount);
            #endregion

            shaduler.StartProcessorWork(); 

            #region TCP CONNECTION
            const string ip = "127.0.0.1";
            const int port = 8080;
            var tcpEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            var tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Bind(tcpEndPoint);
            tcpSocket.Listen(5);

            while (true)
            {
                Console.WriteLine("Жду задачи...");

                Socket listener = tcpSocket.Accept();
                countOfConnectedUsers += 1;

                Thread userProcessingThread = new Thread(userProcessing);
                userProcessingThread.Name = $"User {countOfConnectedUsers} thread";
                userProcessingThread.Start(listener);
            }
            #endregion
        }
    }
}

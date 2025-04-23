using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Task_generator_system;

namespace Shaduler_and_processor_system
{
    internal class Program
    {
        static Queue<Socket> userQueue = new Queue<Socket>();
        static object queueLock = new object();

        static void ProcessQueue(object? shad)
        {
            if (shad == null)
                return;

            Shaduler shaduler = (Shaduler)shad;

            while (true)
            {
                Socket? nextUser = null;

                lock (queueLock)
                {
                    if (userQueue.Count > 0 && shaduler.UserSocket == null)
                    {
                        nextUser = userQueue.Dequeue();
                        Console.WriteLine("Пользователь извлечен из очереди для обработки");
                    }
                }

                if (nextUser != null)
                {
                    try
                    {
                        shaduler.UserSocket = nextUser;
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

            Shaduler shaduler = new Shaduler(workerCount);
            #endregion

            shaduler.StartProcessorWork(); 

            #region TCP CONNECTION
            const string ip = "127.0.0.1";
            const int port = 8080;
            var tcpEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            var tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Bind(tcpEndPoint);
            tcpSocket.Listen(5);

            Thread queueProcessingThread = new Thread(ProcessQueue);
            queueProcessingThread.Start(shaduler);

            while (true)
            {
                Console.WriteLine("Жду задачи...");

                Socket listener = tcpSocket.Accept();

                lock (queueLock)
                {
                    userQueue.Enqueue(listener);
                    Console.WriteLine("Новый пользователь добавлен в очередь");
                }
            }
            #endregion
        }
    }
}

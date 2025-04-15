using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Task_generator_system;

namespace Shaduler_and_processor_system
{
    public struct TasksMetric
    {
        public Stopwatch stopwatch;
        public int countOfTasks;
        public int countOfInterruptedTasks;
    }

    internal class Program
    {
        static void Main(string[] args)
        {
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

            while (true)
            {
                Console.WriteLine("Жду задачи: ");
                Socket listener = tcpSocket.Accept();
                Shaduler.AddUserSocket(listener);
                byte[] buffer = new byte[256];
                int size = 0;
                StringBuilder data = new StringBuilder();

                do
                {
                    size = listener.Receive(buffer);
                    data.Append(Encoding.UTF8.GetString(buffer, 0, size));
                }
                while (listener.Available > 0);

                TaskGenerator? taskGenerator = TaskGenerator.TryCreate(data.ToString());
                if (taskGenerator == null)
                {
                    listener.Send(Encoding.UTF8.GetBytes("BAD DATA"));
                }
                else
                {
                    //listener.Send(Encoding.UTF8.GetBytes("OK"));
                    List<PriorityTask> priorityTasks = taskGenerator.GetTasks();
                    foreach (PriorityTask priorityTask in priorityTasks)
                    {
                        shaduler.Enqueue(priorityTask);
                    }
                    priorityTasks.Clear();
                }

                Console.WriteLine(data);

                //listener.Shutdown(SocketShutdown.Both);
                //listener.Close();
            }
            #endregion
        }
    }
}

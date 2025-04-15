using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TasksDescriptorModule;

namespace Task_generator_system
{
    internal class Program
    {
        public static TasksDescriptor makeTaskDescriptor()
        {
            string? inputString;

            uint amountOfTasks = 0;
            uint interruptionChance = 0;
            uint minExecutionTimeInMilliseconds = 0;
            uint maxExecutionTimeInMilliseconds = 0;

            while (amountOfTasks == 0)
            {
                Console.Write("Количество задач (5): ");
                inputString = Console.ReadLine();
                if (inputString == null || inputString == "")
                    amountOfTasks = 5;
                else
                {
                    try
                    {
                        amountOfTasks = Convert.ToUInt32(inputString);
                    }
                    catch (OverflowException ex)
                    {
                        Console.WriteLine(ex.Message);
                        amountOfTasks = 0;
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine(ex.Message);
                        amountOfTasks = 0;
                    }
                }
            }

            while (interruptionChance == 0)
            {
                Console.Write("Вероятность прерывания задачи 0-100 (10): ");
                inputString = Console.ReadLine();
                if (inputString == null || inputString == "")
                    interruptionChance = 10;
                else
                {
                    try
                    {
                        interruptionChance = Convert.ToUInt32(inputString);

                        if (interruptionChance > 100)
                            throw new OverflowException("The value of interruption chance cannot be > 100");
                    }
                    catch (OverflowException ex)
                    {
                        Console.WriteLine(ex.Message);
                        interruptionChance = 0;
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine(ex.Message);
                        interruptionChance = 0;
                    }
                }
            }


            while (minExecutionTimeInMilliseconds == 0)
            {
                Console.Write("Минимальное время выполнения одной задачи в миллисекундах (1000): ");
                inputString = Console.ReadLine();
                if (inputString == null || inputString == "")
                    minExecutionTimeInMilliseconds = 1000;
                else
                {
                    try
                    {
                        minExecutionTimeInMilliseconds = Convert.ToUInt32(inputString);
                    }
                    catch (OverflowException ex)
                    {
                        Console.WriteLine(ex.Message);
                        minExecutionTimeInMilliseconds = 0;
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine (ex.Message);
                        minExecutionTimeInMilliseconds = 0;
                    }
                }
            }

            while (maxExecutionTimeInMilliseconds < minExecutionTimeInMilliseconds)
            {
                Console.Write("Максимальное время выполнения одной задачи в миллисекундах (1000): ");
                inputString = Console.ReadLine();
                if (inputString == null || inputString == "")
                    maxExecutionTimeInMilliseconds = 1000;
                else
                {
                    try
                    {
                        maxExecutionTimeInMilliseconds = Convert.ToUInt32(inputString);
                    }
                    catch (OverflowException ex)
                    {
                        Console.WriteLine(ex.Message);
                        maxExecutionTimeInMilliseconds = 0;
                    }
                    catch (FormatException ex)
                    {
                        Console.WriteLine (ex.Message);
                        maxExecutionTimeInMilliseconds = 0;
                    }
                }
            }

            return new TasksDescriptor(amountOfTasks, interruptionChance, minExecutionTimeInMilliseconds, maxExecutionTimeInMilliseconds);
        }

        static bool IsValidIPv4(string? ip)
        {
            if (ip == null)
                return false;
            string pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
            return Regex.IsMatch(ip, pattern);
        }

        public static void sendToScheduler(TasksDescriptor taskDescriptor)
        {
            string? inputString;

            string ip = "";
            int port = 0;

            #region IP_SET
            Console.Write("IP адрес (127.0.0.1): ");
            inputString = Console.ReadLine();
            if (inputString == null || inputString == "")
            {
                ip = "127.0.0.1";
            }
            else
            {
                while (!IsValidIPv4(inputString))
                {
                    Console.WriteLine("Введёный IP адрес не верный!");
                    Console.WriteLine("IP адрес (127.0.0.1): ");
                    inputString = Console.ReadLine();
                    if (inputString == null || inputString == "")
                    {
                        ip = "127.0.0.1";
                    }
                }
            }
            #endregion

            #region PORT_SET
            Console.Write("Порт (8080): ");
            inputString = Console.ReadLine();
            if (inputString == null || inputString == "")
            {
                port = 8080;
            }
            else
            {
                port = Convert.ToInt32(inputString);
                while (port <= 0)
                {
                    Console.WriteLine("Порт не верный!");
                    Console.WriteLine("Порт (8080): ");
                    inputString = Console.ReadLine();
                    if (inputString == null || inputString == "")
                        port = 8080;
                    else
                        port = Convert.ToInt32(inputString);
                }
            }
            #endregion

            var tcpEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            var tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Отправка по сокету
            string json = JsonSerializer.Serialize(taskDescriptor);
            var data = Encoding.UTF8.GetBytes(json);

            try
            {
                tcpSocket.Connect(tcpEndPoint);
                tcpSocket.Send(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
                return;
            }

            var buffer = new byte[256];
            var size = 0; 
            var answer = new StringBuilder();

            do
            {
                size = tcpSocket.Receive(buffer);
                answer.Append(Encoding.UTF8.GetString(buffer, 0, size));
            }
            while (tcpSocket.Available > 0);

            Console.WriteLine(answer);

            tcpSocket.Shutdown(SocketShutdown.Both);
            tcpSocket.Close();
        }

        static async Task Main(string[] args)
        {

            string? command;
            TasksDescriptor? taskDescriptor = null;

            while (true)
            {
                Console.Write(">>> ");
                command = Console.ReadLine();
                switch (command)
                {
                    case "exit":
                        goto exit;
                    case null:
                        goto exit;
                    case "help":
                        Console.WriteLine("mkgen : сделать описатель задач с заданными параметрами");
                        Console.WriteLine("send : отправить описатель задач на шедуллер по TCP");
                        Console.WriteLine("chgen : вывести информацию по сделанному описателю");
                        Console.WriteLine("delgen : удалить описатель");
                        break;
                    case "mkgen":
                        taskDescriptor = makeTaskDescriptor();
                        break;
                    case "chgen":
                        if (taskDescriptor is null)
                        {
                            Console.WriteLine("У вас нет созданного генератора");
                        }
                        else
                        {
                            Console.WriteLine(taskDescriptor.ToString());
                        }
                        break;
                    case "send":
                        if (taskDescriptor is null)
                        {
                            Console.WriteLine("У вас нет созданного генератора");
                            break;
                        }

                        sendToScheduler(taskDescriptor);

                        //tasks = taskGenerator.getTasks();

                        //foreach (PriorityTask task in tasks)
                        //{
                        //    try
                        //    {
                        //        await task.execute();
                        //    }
                        //    catch (OperationCanceledException ex)
                        //    {
                        //        Console.WriteLine($"Задача с приоритетом {task.Priority} была отменена. Токен: {ex.CancellationToken}");
                        //    }
                        //}
                        break;
                    case "delgen":
                        taskDescriptor = null;
                        break;

                    default:
                        Console.WriteLine("Нет такой команды! Воспользуйтесь \"help\"");
                        break;
                }
            }
        exit:
            Console.ReadLine();

            //TaskGenerator tg = new TaskGenerator(5, 50, 1000);

            //List<PriorityTask> tasks = tg.getTasks();

            //foreach (PriorityTask task in tasks)
            //{
            //    try
            //    {
            //        await task.execute();
            //    }
            //    catch (OperationCanceledException ex)
            //    {
            //        Console.WriteLine($"Задача с приоритетом {task.Priority} была отменена. Токен: {ex.CancellationToken}");
            //    }
            //}
        }
    }
}

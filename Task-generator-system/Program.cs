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
        public static bool InputFromConsole(
            out uint value,
            string msg,
            uint defaultValue,
            string errorMsg,
            Func<uint, bool>? validationFunction = null)
        {
            bool flag = false;
            value = 0;

            string? inputString;

            Console.Write(msg);
            inputString = Console.ReadLine();
            if (inputString == null || inputString == "")
            {
                value = defaultValue;
                if (validationFunction != null && !validationFunction(value))
                    flag = false;
                else
                    flag = true;
            }
            else
            {
                try
                {
                    value = Convert.ToUInt32(inputString);

                    if (validationFunction != null && !validationFunction(value))
                    {
                        throw new OverflowException(errorMsg);
                    }

                    flag = true;
                }
                catch (OverflowException ex)
                {
                    Console.WriteLine(ex.Message);
                    value = 0;
                }
                catch (FormatException ex)
                {
                    Console.WriteLine(ex.Message);
                    value = 0;
                }
            }

            return flag;
        }

        public static TasksDescriptor MakeTaskDescriptor()
        {
            uint amountOfTasks = 0;
            uint interruptionChance = 0;
            uint minExecutionTimeInMilliseconds = 0;
            uint maxExecutionTimeInMilliseconds = 0;

            // Количество задач
            while (!InputFromConsole(out amountOfTasks, 
                "Количество задач (5): ", 
                5, 
                "Должно быть > 0",
                value => value > 0)) ;

            // Вероятность прерывания
            while (!InputFromConsole(out interruptionChance,
                "Вероятность прерывания задачи 0-100 (10): ",
                10,
                "Значение вероятности прерывания не может быть > 100",
                value => value >= 0 && value <= 100)) ;

            // Минимальное время выполнения задачи
            while (!InputFromConsole(out minExecutionTimeInMilliseconds,
                "Минимальное время выполнения одной задачи в миллисекундах (1000): ",
                1000,
                "Должно быть > 0",
                value => value > 0)) ;

            // Максимальное время выполнения задачи
            while (!InputFromConsole(out maxExecutionTimeInMilliseconds,
                "Максимальное время выполнения одной задачи в миллисекундах (1000): ",
                1000,
                "Максимальное время не может быть меньше минимального",
                value => value >= minExecutionTimeInMilliseconds)) ;

            return new TasksDescriptor(amountOfTasks, interruptionChance, minExecutionTimeInMilliseconds, maxExecutionTimeInMilliseconds);
        }

        static bool IsValidIPv4(string? ip)
        {
            if (ip == null)
                return false;
            string pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
            return Regex.IsMatch(ip, pattern);
        }

        static void IpAndPortSet(out string ip, out int port)
        {
            string? inputString;

            ip = "";
            port = 0;

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
        }

        static void ReceiveLoop(Socket socket)
        {
            byte[] buffer = new byte[256];
            int size = 0;
            StringBuilder answer = new StringBuilder();

            try
            {
                do
                {
                    size = socket.Receive(buffer);
                    if (size > 0)
                    {
                        string answerString = Encoding.UTF8.GetString(buffer, 0, size);
                        if (answerString != "200")
                            answer.Append(answerString);
                        else
                            break;

                        Console.Write(answerString);
                        Console.Write(">>> ");
                    }

                } while (size > 0 && !answer.ToString().Contains("200"));
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Ошибка при приёме данных: {ex.Message}");
            }
            finally
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
        }

        public static async void SendToSchedulerAsync(TasksDescriptor taskDescriptor, string ip, int port)
        {
            IPEndPoint tcpEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                tcpSocket.Connect(tcpEndPoint);

                string json = JsonSerializer.Serialize(taskDescriptor);
                byte[] data = Encoding.UTF8.GetBytes(json);

                tcpSocket.Send(data);

                await Task.Run(() => ReceiveLoop(tcpSocket));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения {ex.Message}");
                return;
            }
        }

        public static void MakeBenchmarkTest()
        {
            string ip = "";
            int port = 0;

            IpAndPortSet(out ip, out port);

            uint minAmountOfTasks = 0;
            uint maxAmountOfTasks = 0;
            uint stepAmountOfTasks = 0;
            uint interruptionChance = 0;
            uint minExecutionTimeInMilliseconds = 0;
            uint maxExecutionTimeInMilliseconds = 0;

            // Минимальное количество задач
            while (!InputFromConsole(out minAmountOfTasks,
                "Стартовое значение количества задач (10): ",
                10,
                "Должно быть > 0",
                value => value > 0)) ;

            // Максимальное количество задач
            while (!InputFromConsole(out maxAmountOfTasks,
                "Конечное значение количества задач (100): ",
                100,
                "Должно быть > 0",
                value => value >= minAmountOfTasks)) ;

            // Шаг прибавления количества задач
            while (!InputFromConsole(out stepAmountOfTasks,
                "Шаг количества задач (10): ",
                10,
                "Должно быть > 0",
                value => value > 0)) ;

            // Вероятность прерывания
            while (!InputFromConsole(out interruptionChance,
                "Вероятность прерывания задачи 0-100 (0): ",
                0,
                "Значение вероятности прерывания не может быть > 100",
                value => value >= 0 && value <= 100)) ;

            // Минимальное время выполнения задачи
            while (!InputFromConsole(out minExecutionTimeInMilliseconds,
                "Минимальное время выполнения одной задачи в миллисекундах (2000): ",
                2000,
                "Должно быть > 0",
                value => value > 0)) ;

            // Максимальное время выполнения задачи
            while (!InputFromConsole(out maxExecutionTimeInMilliseconds,
                "Максимальное время выполнения одной задачи в миллисекундах (3000): ",
                3000,
                "Максимальное время не может быть меньше минимального",
                value => value > minExecutionTimeInMilliseconds)) ;

            for(uint i = minAmountOfTasks; i <= maxAmountOfTasks ; i += stepAmountOfTasks)
            {
                TasksDescriptor tasksDescriptor = new TasksDescriptor(i, interruptionChance, minExecutionTimeInMilliseconds, maxExecutionTimeInMilliseconds);
                SendToSchedulerAsync(tasksDescriptor, ip, port);
            }
        }

        static void Main(string[] args)
        {
            string? command;
            TasksDescriptor? taskDescriptor = null;
            string ip = "";
            int port = 0;

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
                        Console.WriteLine("setsocket : задать ip и порт сервера");
                        Console.WriteLine("send : отправить описатель задач на шедуллер по TCP (предварительно ip и порт должны быть заданы)");
                        Console.WriteLine("checkgen : вывести информацию по сделанному описателю");
                        Console.WriteLine("benchmark : провести бенчмарк системы");
                        Console.WriteLine("delgen : удалить описатель");
                        Console.WriteLine("exit : выйти из программы (ctrl + Z)");
                        break;
                    case "mkgen":
                        taskDescriptor = MakeTaskDescriptor();
                        break;
                    case "checkgen":
                        if (taskDescriptor is null)
                        {
                            Console.WriteLine("У вас нет созданного генератора");
                        }
                        else
                        {
                            Console.WriteLine(taskDescriptor.ToString());
                        }
                        break;
                    case "setsocket":
                        IpAndPortSet(out ip, out port);
                        break;
                    case "send":
                        if (taskDescriptor is null || ip == "" || port == 0)
                        {
                            Console.WriteLine("У вас нет созданного генератора, либо некорректно введён порт и ip");
                            break;
                        }

                        SendToSchedulerAsync(taskDescriptor, ip, port);
                        break;
                    case "benchmark":
                        MakeBenchmarkTest();
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
        }
    }
}

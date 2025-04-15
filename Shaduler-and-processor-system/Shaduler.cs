using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Task_generator_system;

namespace Shaduler_and_processor_system
{
    public class Shaduler
    {
        // Очередь задач по приоритетам
        private readonly SortedList<int, ConcurrentQueue<PriorityTask>> _queues;

        // Объект для потокобезопасного добавления и извлечения задач из очереди
        private readonly object _lock = new();

        // Флаг обозначающий вызвана ли функция StartProcessorWork() или нет
        private bool _isProcessorWork;

        // Количество процессов
        private uint _countWorkers;
        
        // Таймер, замерящий время выполнения задач
        private Stopwatch _stopWatch;

        // Механизм синхронизации. Позволяет блокировать потоки до тех пор
        // пока не будет вызван метод Set. После вызова Set все заблокированные потоки возобновят выполнение.
        private readonly ManualResetEventSlim _taskAvailable = new ManualResetEventSlim(false);

        // Количество потоков, находящихся в состоянии ожидания
        private int _waitingThreads = 0;

        // Сокет текущего подключенного пользователя
        private static Socket? _userSocket;

        private Dictionary<Socket, TasksMetric> _taskMetrics;

        public Shaduler(uint countWorkers) 
        {
            _queues = new SortedList<int, ConcurrentQueue<PriorityTask>>();
            _countWorkers = countWorkers;
            _isProcessorWork = false;
            _stopWatch = new Stopwatch();
            _taskMetrics = new Dictionary<Socket, TasksMetric>();
        }

        public void Enqueue(PriorityTask task)
        {
            lock (_lock)
            {
                if (_queues.Count == 0)
                {
                    _stopWatch = Stopwatch.StartNew();
                }

                if (!_queues.ContainsKey(task.Priority))
                {
                    _queues[task.Priority] = new ConcurrentQueue<PriorityTask>();
                }
                _queues[task.Priority].Enqueue(task);
            }

            _taskAvailable.Set();
        }

        public PriorityTask? Dequeue()
        {
            lock (_lock)
            {
                if (_queues.Count == 0)
                {
                    return null;
                }

                int maxPriority = _queues.Keys[_queues.Count - 1];

                if (_queues[maxPriority].TryDequeue(out var task))
                {
                    if (_queues[maxPriority].IsEmpty)
                    {
                        _queues.Remove(maxPriority);
                    }

                    return task;
                }

                return null;
            }
        }

        public void ClearTasks ()
        {
            _queues.Clear();
        }

        public void StartProcessorWork()
        {
            if (_isProcessorWork) { return; }

            for (int i = 0; i < _countWorkers; i++)
            {
                Thread taskThread = new Thread(CompleteTask)
                {
                    Name = "Поток " + i.ToString() + ":",
                    IsBackground = true
                };
                taskThread.Start(taskThread.Name);
            }

            _isProcessorWork = true;
        }

        public override string ToString()
        {
            if (_queues.Count == 0)
            {
                return "no queue";
            }

            StringBuilder sb = new StringBuilder();
            foreach (var key in _queues.Keys)
            {
                sb.Append("Приоритет: " + key + "\n");
                ConcurrentQueue<PriorityTask> currentQueue = _queues[key];
                foreach (var task in currentQueue)
                {
                    sb.Append("\tID задачи: " + task.ToString() + "\n");
                }
            }

            return sb.ToString();
        }

        private async void CompleteTask(object? message)
        {
            while (true)
            {
                PriorityTask? task = Dequeue();

                if (task is null) 
                {
                    Interlocked.Increment(ref _waitingThreads);

                    if (_queues.Count == 0 &&  _waitingThreads == _countWorkers)
                    {
                        _stopWatch.Stop();
                        if ((int)_stopWatch.ElapsedMilliseconds > 0)
                        {
                            string str = $"Все задачи были выполнены за {(int)_stopWatch.ElapsedMilliseconds}";
                            Console.WriteLine(str);
                            _userSocket?.Send(Encoding.UTF8.GetBytes(str));
                            _userSocket?.Shutdown(SocketShutdown.Both);
                            _userSocket?.Close();
                        }
                    }

                    _taskAvailable.Wait();
                    await Task.Delay(10);
                    _taskAvailable.Reset();

                    Interlocked.Decrement(ref _waitingThreads);
                    continue; 
                }

                try
                {
                    await Task.Run(() => task.Execute());
                }
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine($"Задача {task.Id} с приоритетом {task.Priority} была отменена. Токен: {ex.CancellationToken}");
                }
            }
        }

        public static void AddUserSocket(Socket socket)
        {
            _userSocket = socket;
        }
    }
}

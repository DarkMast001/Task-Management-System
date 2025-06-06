﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Task_generator_system;

namespace Shaduler_and_processor_system
{
    public class Shaduler
    {
        // Очередь задач по приоритетам
        private readonly SortedList<int, ConcurrentQueue<PriorityTask>> _queues = new SortedList<int, ConcurrentQueue<PriorityTask>>();

        // Объект для потокобезопасного добавления и извлечения задач из очереди
        private readonly object _lock = new();

        // Флаг, обозначающий вызвана ли функция StartProcessorWork() или нет
        private bool _isProcessorWork = false;

        // Количество процессов
        private uint _countWorkers;
        
        // Таймер, замерящий время выполнения задач
        private Stopwatch _stopWatch = new Stopwatch();

        // Механизм синхронизации. Позволяет блокировать потоки до тех пор
        // пока не будет вызван метод Set. После вызова Set все заблокированные потоки возобновят выполнение.
        private readonly ManualResetEventSlim _taskAvailable = new ManualResetEventSlim(false);

        // Количество потоков, находящихся в состоянии ожидания
        private int _waitingThreads = 0;

        // Сокет текущего подключенного пользователя
        private Socket? _userSocket;

        // Количество задач, необходимых к выполнению
        private int _countOfTasks = 0;

        // Событие, которое выполнится в момент, когда все задачи будут обработаны
        private event Action<Socket, int, int>? _notifyingUserOfCompletionTasks;

        public Shaduler(uint countWorkers) 
        {
            _countWorkers = countWorkers;
        }

        public Socket? UserSocket
        {
            get => _userSocket;
            set
            {
                _userSocket = value;
                if (value == null)
                    _notifyingUserOfCompletionTasks = null;
            }
        }

        public event Action<Socket, int, int>? NotifyingUserOfCompletionTasks
        {
            add => _notifyingUserOfCompletionTasks += value;
            remove => _notifyingUserOfCompletionTasks -= value;
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
                _countOfTasks++;
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

                    if (_queues.Count == 0 && _waitingThreads == _countWorkers)
                    {
                        _stopWatch.Stop();
                        if ((int)_stopWatch.ElapsedMilliseconds > 0)
                        {
                            if (_userSocket == null)
                            {
                                Console.WriteLine("Сокет пользователя null");
                                _countOfTasks = 0;
                                return;
                            }
                            if (_notifyingUserOfCompletionTasks == null)
                            {
                                _userSocket.Send(Encoding.UTF8.GetBytes("200"));
                                _userSocket.Shutdown(SocketShutdown.Both);
                                _userSocket.Close();
                                return;
                            }
                            _notifyingUserOfCompletionTasks.Invoke(_userSocket, _countOfTasks, (int)_stopWatch.ElapsedMilliseconds);
                            _countOfTasks = 0;
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
    }
}

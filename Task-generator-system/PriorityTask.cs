using System.Net.Sockets;

namespace Task_generator_system
{
    public class PriorityTask
    {
        private readonly int _priority;
        private readonly Task _task;
        private readonly int _executionTime;

        private Socket? _socket;

        public PriorityTask(Action action, int priority, int executionTime)
        {
            _task = new Task(action);
            _priority = priority;
            _executionTime = executionTime;
        }

        public int Priority => _priority;

        public int Id => _task.Id;

        public Socket? Socket
        {
            get => _socket;
            set => _socket = value;
        }

        public async Task Execute()
        {
            try
            {
                _task.Start();
                Console.WriteLine($"Задача {Id} с приоритетом {_priority} выполняется {_executionTime} мс...");
                await _task;
                Console.WriteLine($"Задача {Id} с приоритетом {_priority} завершена.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Задача {Id} с приоритетом {_priority} прервана.");
                throw;
            }
        }

        public override string ToString()
        {
            return $"{_task.Id}";
        }
    }
}

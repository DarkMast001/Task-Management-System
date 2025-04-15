using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using TasksDescriptorModule;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Task_generator_system
{
    public class TaskGenerator
    {
        private readonly int _amountOfTasks;
        private readonly int _interruptionChance;
        private readonly int _minExecutionTimeInMilliseconds;
        private readonly int _maxExecutionTimeInMilliseconds;

        private List<PriorityTask> _tasks;

        private TaskGenerator(TasksDescriptor tasksDescriptor)
        {
            _tasks = CreateTasks((int)tasksDescriptor.AmountOfTasks, (int)tasksDescriptor.InterruptionChance, 
                (int)tasksDescriptor.MinExecutionTimeInMilliseconds, (int)tasksDescriptor.MaxExecutionTimeInMilliseconds);
        }

        public TaskGenerator(int amountOfTasks, int interruptionChance = 10, int minExecutionTimeInMilliseconds = 1000, int maxExecutionTimeInMilliseconds = 1000)
        {
            _tasks = CreateTasks(amountOfTasks, interruptionChance, minExecutionTimeInMilliseconds, maxExecutionTimeInMilliseconds);
        }

        public List<PriorityTask> GetTasks()
        {
            return _tasks;
        }

        public List<PriorityTask> CreateTasks(int amountOfTasks, int interruptionChance = 10, int minExecutionTimeInMilliseconds = 1000, int maxExecutionTimeInMilliseconds = 1000)
        {
            List <PriorityTask> tasks = new List<PriorityTask>(amountOfTasks);

            Random random = new Random();
            for (int i = 0; i < amountOfTasks; i++)
            {
                int executionTime = random.Next(minExecutionTimeInMilliseconds, maxExecutionTimeInMilliseconds);
                int priority = random.Next(1, 11);
                bool shouldCancel = random.Next(1, 101) <= interruptionChance;

                PriorityTask task = new PriorityTask(() =>
                {
                    if (shouldCancel)
                    {
                        throw new OperationCanceledException();
                    }

                    Thread.Sleep(executionTime);
                }, priority, executionTime);

                tasks.Add(task);
            }

            return tasks;
        }

        public static TaskGenerator? TryCreate(string json)
        {
            TasksDescriptor? tasksDescriptor = JsonSerializer.Deserialize<TasksDescriptor>(json);
            if (tasksDescriptor == null)
            {
                return null;
            }
            return new TaskGenerator(tasksDescriptor);
        }

        public override string ToString()
        {
            return
                $"Количество задач: {_amountOfTasks}\n" +
                $"Вероятность прерывания задачи: {_interruptionChance}\n" +
                $"Минимальное время выполнения: {_minExecutionTimeInMilliseconds}\n" +
                $"Максимальное время выполнения: {_maxExecutionTimeInMilliseconds}";
            ;
        }
    }
}

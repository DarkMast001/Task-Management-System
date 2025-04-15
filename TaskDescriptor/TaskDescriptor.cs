using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TasksDescriptorModule
{
    public class TasksDescriptor
    {
        private readonly uint _amountOfTasks;
        private readonly uint _interruptionChance;
        private readonly uint _minExecutionTimeInMilliseconds;
        private readonly uint _maxExecutionTimeInMilliseconds;

        public TasksDescriptor(uint amountOfTasks, uint interruptionChance = 10, uint minExecutionTimeInMilliseconds = 1000, uint maxExecutionTimeInMilliseconds = 1000)
        {
            _amountOfTasks = amountOfTasks;
            _interruptionChance = interruptionChance;
            _minExecutionTimeInMilliseconds = minExecutionTimeInMilliseconds;
            _maxExecutionTimeInMilliseconds = maxExecutionTimeInMilliseconds;

            if (minExecutionTimeInMilliseconds > maxExecutionTimeInMilliseconds)
            {
                _minExecutionTimeInMilliseconds = maxExecutionTimeInMilliseconds;
                _maxExecutionTimeInMilliseconds = minExecutionTimeInMilliseconds;
            }
        }

        public uint AmountOfTasks
        {
            get => _amountOfTasks;
        }

        public uint InterruptionChance
        {
            get => _interruptionChance;
        }

        public uint MinExecutionTimeInMilliseconds
        {
            get => _minExecutionTimeInMilliseconds;
        }

        public uint MaxExecutionTimeInMilliseconds 
        {
            get => _maxExecutionTimeInMilliseconds;
        }

        public override string ToString()
        {
            return
                $"Количество задач: {_amountOfTasks}\n" +
                $"Вероятность прерывания задачи: {_interruptionChance}%\n" +
                $"Минимальное время выполнения: {_minExecutionTimeInMilliseconds}ms\n" +
                $"Максимальное время выполнения: {_maxExecutionTimeInMilliseconds}ms";
        }
    }
}

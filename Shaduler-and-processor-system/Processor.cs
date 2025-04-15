using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shaduler_and_processor_system
{
    public class Processor
    {
        private uint _countWorkers;

        public Processor(uint countWorkers)
        {
            _countWorkers = countWorkers;
        }

        public uint CountWorkers
        {
            get => _countWorkers;
        }

        public void Start()
        {

        }
    }
}

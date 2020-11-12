using System;
using System.Collections.Generic;
using System.Text;

namespace horizon.Legacy
{
    public class HorizonOptions
    {
        /// <summary>
        /// Default buffer size for worker io operations. (default) ReducedLatency - 131072 bytes
        /// </summary>
        public int DefaultBufferSize = (int) OptimizedBuffer.ReducedLatency;
        
        public enum OptimizedBuffer
        {
            ReducedLatency = 131072,
            MaximalThroughput = 262144
        }
    }
}

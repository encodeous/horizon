using System;
using System.Collections.Generic;
using System.Text;

namespace horizon
{
    public class HorizonResponse
    {
        /// <summary>
        /// Client token Hashed with the same request time, but a different salt
        /// </summary>
        public byte[] ClientTokenHash;

        /// <summary>
        /// Salt should be randomly generated to be secure
        /// </summary>
        public byte[] Salt;
    }
}

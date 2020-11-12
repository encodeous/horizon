using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace horizon.Protocol
{
    public interface IDataTransformer
    {
        /// <summary>
        /// The Transformer Order is the order the data is encoded, the smallest number goes first and so on. When the data is decoded, the reverse order is followed.
        /// </summary>
        int TransformerOrder { get; }

        Stream CreateTransformationStream(Stream original);

        Stream CreateReverseTransformationStream(Stream original);
    }
}

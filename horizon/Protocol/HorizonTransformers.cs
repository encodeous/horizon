using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace horizon.Protocol
{
    /// <summary>
    /// Allows the hotswapping of streams like during encryption and compression
    /// TODO: Add Compression Capabilities
    /// </summary>
    public class HorizonTransformers
    {
        public Stream WriteStream, ReadStream;

        public HorizonTransformers(Stream stream)
        {
            WriteStream = stream;
            ReadStream = stream;
        }

        public void AddTransformer(IDataTransformer transformer)
        {
            WriteStream = transformer.CreateTransformationStream(WriteStream);
            ReadStream = transformer.CreateReverseTransformationStream(ReadStream);
        }
    }
}

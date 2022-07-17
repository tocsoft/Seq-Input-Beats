using System;

namespace Seq.Input.Beats
{
    public class InvalidFrameProtocolException : Exception
    {
        public InvalidFrameProtocolException(string message)
            : base(message)
        {
        }
    }
}

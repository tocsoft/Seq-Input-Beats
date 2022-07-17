namespace Seq.Input.Beats
{
    public class Protocol
    {
        public const byte VERSION_1 = (byte)'1';
        public const byte VERSION_2 = (byte)'2';

        public const byte CODE_WINDOW_SIZE = (byte)'W';
        public const byte CODE_JSON_FRAME = (byte)'J';
        public const byte CODE_COMPRESSED_FRAME = (byte)'C';
        public const byte CODE_FRAME = (byte)'D';

        public static int Version(byte versionRead)
        {
            if (Protocol.VERSION_2 == versionRead)
            {
                return 2;
            }
            else if (Protocol.VERSION_1 == versionRead)
            {
                return 1;
            }
            throw new InvalidFrameProtocolException("Invalid version of beats protocol: " + versionRead);
        }
    }
}

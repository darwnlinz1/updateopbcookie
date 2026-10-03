using MessagePack;
using PeterO.Cbor;
using RuriLib.Functions.Formats;
using RuriLib.LS;
using System;
using System.Text;
using System.Windows.Media;

namespace RuriLib.Blocks
{
    /// <summary>
    /// The operation mode for binary codecs.
    /// </summary>
    public enum BinaryCodecMode
    {
        /// <summary>Encodes a JSON string into a Base64-wrapped binary payload.</summary>
        Encode,

        /// <summary>Decodes a binary payload (Base64 string or raw bytes) into a readable JSON string.</summary>
        Decode
    }

    /// <summary>
    /// The supported binary serialization format.
    /// </summary>
    public enum BinaryCodecFormat
    {
        /// <summary>Concise Binary Object Representation (RFC 8949).</summary>
        CBOR,

        /// <summary>MessagePack binary serialization.</summary>
        MSGPACK,

        /// <summary>Google Protocol Buffers raw wire format (Decode only).</summary>
        PROTOBUF
    }

    /// <summary>
    /// A block that encodes and decodes binary formats (CBOR, MessagePack, Protobuf Wire).
    /// </summary>
    public class BlockBinaryCodec : BlockBase
    {
        #region Properties
        private BinaryCodecMode codecMode = BinaryCodecMode.Decode;
        /// <summary>The operation mode (Encode or Decode).</summary>
        public BinaryCodecMode CodecMode { get { return codecMode; } set { codecMode = value; OnPropertyChanged(); } }

        private BinaryCodecFormat codecFormat = BinaryCodecFormat.CBOR;
        /// <summary>The binary format (CBOR, MSGPACK, PROTOBUF).</summary>
        public BinaryCodecFormat CodecFormat { get { return codecFormat; } set { codecFormat = value; OnPropertyChanged(); } }

        private string inputString = "<SOURCE>";
        /// <summary>The input string (JSON for Encode, Base64/raw for Decode).</summary>
        public string InputString { get { return inputString; } set { inputString = value; OnPropertyChanged(); } }

        private string variableName = "BINARY_DATA";
        /// <summary>The name of the output variable.</summary>
        public string VariableName { get { return variableName; } set { variableName = value; OnPropertyChanged(); } }

        private bool isCapture = false;
        /// <summary>Whether the output variable should be marked for capture.</summary>
        public bool IsCapture { get { return isCapture; } set { isCapture = value; OnPropertyChanged(); } }

        private bool createEmpty = true;
        /// <summary>Whether to create an empty variable if the transformation fails or produces empty output.</summary>
        public bool CreateEmpty { get { return createEmpty; } set { createEmpty = value; OnPropertyChanged(); } }
        #endregion

        /// <summary>
        /// Creates a new BlockBinaryCodec instance.
        /// </summary>
        public BlockBinaryCodec()
        {
            Label = "BINARY CODEC";
        }

        /// <inheritdoc />
        public override BlockBase FromLS(string line)
        {
            var input = line.Trim();

            // Parse the label if present
            if (input.StartsWith("#"))
                Label = LineParser.ParseLabel(ref input);

            // Consume BINARYCODEC identifier if still present
            if (input.StartsWith("BINARYCODEC", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("BINARY_CODEC", StringComparison.OrdinalIgnoreCase))
            {
                LineParser.ParseToken(ref input, TokenType.Parameter, true);
            }

            // Parse Mode (Encode / Decode)
            CodecMode = (BinaryCodecMode)LineParser.ParseEnum(ref input, "Mode", typeof(BinaryCodecMode));

            // Parse Format (CBOR / MSGPACK / PROTOBUF)
            CodecFormat = (BinaryCodecFormat)LineParser.ParseEnum(ref input, "Format", typeof(BinaryCodecFormat));

            // Parse Input string
            InputString = LineParser.ParseLiteral(ref input, "InputString");

            // Parse optional booleans (e.g. CreateEmpty=TRUE)
            while (LineParser.Lookahead(ref input) == TokenType.Boolean)
                LineParser.SetBool(ref input, this);

            // Parse arrow and variable info
            if (LineParser.ParseToken(ref input, TokenType.Arrow, false) == string.Empty)
                return this;

            try
            {
                var varType = LineParser.ParseToken(ref input, TokenType.Parameter, true);
                if (varType.ToUpper() == "VAR" || varType.ToUpper() == "CAP")
                    IsCapture = varType.ToUpper() == "CAP";
            }
            catch { throw new ArgumentException("Invalid or missing variable type"); }

            try { VariableName = LineParser.ParseToken(ref input, TokenType.Literal, true); }
            catch { throw new ArgumentException("Variable name not specified"); }

            return this;
        }

        /// <inheritdoc />
        public override string ToLS(bool indent = true)
        {
            var writer = new BlockWriter(GetType(), indent, Disabled);
            writer
                .Label(Label)
                .Token("BINARYCODEC")
                .Token(CodecMode)
                .Token(CodecFormat)
                .Literal(InputString)
                .Boolean(CreateEmpty, nameof(CreateEmpty));

            if (!writer.CheckDefault(VariableName, nameof(VariableName)))
            {
                writer
                    .Arrow()
                    .Token(IsCapture ? "CAP" : "VAR")
                    .Literal(VariableName);
            }

            return writer.ToString();
        }

        /// <inheritdoc />
        public override void Process(BotData data)
        {
            if (data != null && data.LogBuffer != null)
                base.Process(data);

            var resolvedInput = ReplaceValues(InputString, data);
            var output = string.Empty;

            try
            {
                if (CodecMode == BinaryCodecMode.Encode)
                {
                    // ENCODE: JSON string -> Binary bytes -> Base64 string
                    if (string.IsNullOrWhiteSpace(resolvedInput))
                        resolvedInput = "{}";

                    byte[] binaryBytes;
                    switch (CodecFormat)
                    {
                        case BinaryCodecFormat.CBOR:
                            binaryBytes = CBORObject.FromJSONString(resolvedInput).EncodeToBytes();
                            break;

                        case BinaryCodecFormat.MSGPACK:
                            binaryBytes = MessagePackSerializer.FromJson(resolvedInput);
                            break;

                        case BinaryCodecFormat.PROTOBUF:
                            throw new NotSupportedException("Protobuf Encode requires a schema (.proto). Use 'FUNCTION GrpcWebEncode' in FUNCTION block for gRPC-Web string payloads.");

                        default:
                            throw new NotSupportedException($"Unsupported format {CodecFormat}");
                    }

                    output = Convert.ToBase64String(binaryBytes);
                }
                else
                {
                    // DECODE: Base64 string or raw bytes -> Binary bytes -> JSON string
                    if (string.IsNullOrWhiteSpace(resolvedInput))
                    {
                        output = "{}";
                    }
                    else
                    {
                        byte[] rawBytes;
                        try
                        {
                            rawBytes = Convert.FromBase64String(resolvedInput.Trim());
                        }
                        catch
                        {
                            // Fallback to UTF-8 raw bytes if not valid Base64
                            rawBytes = Encoding.UTF8.GetBytes(resolvedInput);
                        }

                        switch (CodecFormat)
                        {
                            case BinaryCodecFormat.CBOR:
                                output = CBORObject.DecodeFromBytes(rawBytes).ToJSONString();
                                break;

                            case BinaryCodecFormat.MSGPACK:
                                output = MessagePackSerializer.ToJson(rawBytes);
                                break;

                            case BinaryCodecFormat.PROTOBUF:
                                output = ProtobufWireDecoder.DecodeToJson(rawBytes);
                                break;

                            default:
                                throw new NotSupportedException($"Unsupported format {CodecFormat}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (data != null)
                    data.Log(new LogEntry($"[BINARYCODEC ERROR] {ex.Message}", Colors.Tomato));
                if (!CreateEmpty)
                    throw;
                output = string.Empty;
            }

            if (data != null)
            {
                InsertVariable(data, IsCapture, output, VariableName, "", "", false, CreateEmpty);
                data.Log(new LogEntry($"Executed BINARYCODEC {CodecMode} {CodecFormat} -> {output}", Colors.DarkOrange));
            }
        }
    }
}

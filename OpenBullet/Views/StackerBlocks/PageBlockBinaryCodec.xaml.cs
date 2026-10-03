using RuriLib.Blocks;
using System;
using System.Windows.Controls;

namespace OpenBulletCE.Views.StackerBlocks
{
    /// <summary>
    /// Interaction logic for PageBlockBinaryCodec.xaml
    /// </summary>
    public partial class PageBlockBinaryCodec : Page
    {
        private BlockBinaryCodec vm;

        public PageBlockBinaryCodec(BlockBinaryCodec block)
        {
            InitializeComponent();
            vm = block;
            DataContext = vm;

            // Populate Modes
            foreach (var m in Enum.GetNames(typeof(BinaryCodecMode)))
                modeCombobox.Items.Add(m);
            modeCombobox.SelectedIndex = (int)vm.CodecMode;

            // Populate Formats
            foreach (var f in Enum.GetNames(typeof(BinaryCodecFormat)))
                formatCombobox.Items.Add(f);
            formatCombobox.SelectedIndex = (int)vm.CodecFormat;

            // Variable type
            varTypeCombobox.SelectedIndex = vm.IsCapture ? 1 : 0;

            UpdateInfoText();
        }

        private void modeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (modeCombobox.SelectedIndex >= 0)
            {
                vm.CodecMode = (BinaryCodecMode)modeCombobox.SelectedIndex;
                UpdateInfoText();
            }
        }

        private void formatCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (formatCombobox.SelectedIndex >= 0)
            {
                vm.CodecFormat = (BinaryCodecFormat)formatCombobox.SelectedIndex;
                UpdateInfoText();
            }
        }

        private void varTypeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (varTypeCombobox.SelectedIndex >= 0)
            {
                vm.IsCapture = varTypeCombobox.SelectedIndex == 1;
            }
        }

        private void UpdateInfoText()
        {
            if (infoTextBlock == null) return;

            string modeDesc = vm.CodecMode == BinaryCodecMode.Decode
                ? "DECODE: Reads binary data (Base64 or raw bytes) and outputs human-readable JSON string."
                : "ENCODE: Reads JSON string and serializes into binary format, outputting Base64 string for HTTP payload.";

            string formatDesc = "";
            switch (vm.CodecFormat)
            {
                case BinaryCodecFormat.CBOR:
                    formatDesc = "Format CBOR (RFC 8949): Compact binary representation used in WebAuthn/FIDO2, mobile telemetry, and IoT.";
                    break;
                case BinaryCodecFormat.MSGPACK:
                    formatDesc = "Format MessagePack: High-performance binary format used across mobile APIs, gaming backends, and microservices.";
                    break;
                case BinaryCodecFormat.PROTOBUF:
                    formatDesc = "Format Protobuf Wire: Decodes raw Protocol Buffers wire packets directly without needing .proto schema files (Decode only).";
                    break;
            }

            infoTextBlock.Text = $"{modeDesc}\n\n{formatDesc}";
        }
    }
}

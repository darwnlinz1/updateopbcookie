using Extreme.Net;
using OpenBulletCE.Views.Main.Runner;
using RuriLib;
using RuriLib.Blocks;
using RuriLib.Functions.Requests;
using RuriLib.Functions.Requests.TlsClient;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace OpenBulletCE.Views.StackerBlocks
{
    /// <summary>
    /// Interaction logic for PageBlockTlsRequest.xaml
    /// </summary>
    public partial class PageBlockTlsRequest : Page
    {
        BlockTlsRequest vm;

        public PageBlockTlsRequest(BlockTlsRequest block)
        {
            InitializeComponent();
            vm = block;
            DataContext = vm;

            foreach (string i in Enum.GetNames(typeof(Extreme.Net.HttpMethod)))
                methodCombobox.Items.Add(i);

            methodCombobox.SelectedIndex = (int)vm.Method;

            foreach (var profile in TlsClientProfiles.AvailableProfiles)
                tlsProfileCombobox.Items.Add(profile);

            tlsProfileCombobox.Text = vm.TlsIdentifier;

            foreach (string t in Enum.GetNames(typeof(RequestType)))
                requestTypeCombobox.Items.Add(t);

            requestTypeCombobox.SelectedIndex = (int)vm.RequestType;

            foreach (string t in Enum.GetNames(typeof(ResponseType)))
                responseTypeCombobox.Items.Add(t);

            responseTypeCombobox.SelectedIndex = (int)vm.ResponseType;

            customCookiesRTB.AppendText(vm.GetCustomCookies());
            customHeadersRTB.AppendText(vm.GetCustomHeaders());
            multipartContentsRTB.AppendText(vm.GetMultipartContents());

            List<string> commonContentTypes = new List<string>()
            {
                "application/x-www-form-urlencoded",
                "application/json",
                "text/plain"
            };

            foreach (var c in commonContentTypes)
                contentTypeCombobox.Items.Add(c);
        }

        private void methodCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            vm.Method = (Extreme.Net.HttpMethod)methodCombobox.SelectedIndex;
        }

        private void tlsProfileCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tlsProfileCombobox.SelectedItem != null)
            {
                vm.TlsIdentifier = tlsProfileCombobox.SelectedItem.ToString();
            }
        }

        private void requestTypeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            vm.RequestType = (RequestType)requestTypeCombobox.SelectedIndex;

            switch (vm.RequestType)
            {
                default:
                    requestTypeTabControl.SelectedIndex = 1;
                    break;

                case RequestType.Standard:
                    requestTypeTabControl.SelectedIndex = 2;
                    break;

                case RequestType.Multipart:
                    requestTypeTabControl.SelectedIndex = 3;
                    break;

                case RequestType.Raw:
                    requestTypeTabControl.SelectedIndex = 4;
                    break;
            }
        }

        private void responseTypeCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            vm.ResponseType = (ResponseType)responseTypeCombobox.SelectedIndex;

            switch (vm.ResponseType)
            {
                default:
                    responseTypeTabControl.SelectedIndex = 0;
                    break;

                case ResponseType.File:
                    responseTypeTabControl.SelectedIndex = 1;
                    break;

                case ResponseType.Base64String:
                    responseTypeTabControl.SelectedIndex = 2;
                    break;
            }
        }

        private void customCookiesRTB_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            vm.SetCustomCookies(customCookiesRTB.Lines());
        }

        private void customHeadersRTB_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            vm.SetCustomHeaders(customHeadersRTB.Lines());
        }

        private void multipartContentsRTB_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            vm.SetMultipartContents(multipartContentsRTB.Lines());
        }
    }
}

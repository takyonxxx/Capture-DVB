using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using DirectShowLib;
using DirectShowLib.BDA;
using DirectShowLib.Utils;
using DirectShowLib.Sample;
using System.Linq;
using System.Net.NetworkInformation;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using DirectShowLib.SBE;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using DShowNET;
using WindowsMediaLib;

namespace Turkay_DTV
{
    public partial class Form1 : Form
    {
        IFilterGraph2 graphBuilder = null;
        IFilterGraph2 pSourceGraph = null;
        ICaptureGraphBuilder2 capBuilder = null;
        DsROTEntry rot = null;
        IBaseFilter networkProvider = null;
        IBaseFilter mpeg2Demux = null;
        IBaseFilter tuner = null;
        IBaseFilter demodulator = null;
        IBaseFilter capture = null;
        IBaseFilter bdaTIF = null;
        IBaseFilter bdaSecTab = null;

        IBaseFilter videoRenderer = null;
        IBaseFilter audioRenderer = null;

        IBaseFilter WMASFWritter = null;
        IBaseFilter myvdecoder = null;
        IBaseFilter myadecoder = null;
        IBaseFilter streamBufferSource = null;

        IVideoWindow vidcapwindow = null;
        IBaseFilter streamBufferSink = null;
        IBaseFilter fileStreamSink = null;
        IStreamBufferSink sink = null;
        IDVBSTuningSpace tuningSpace = null;
        IDVBTuneRequest tuneRequest = null;
        IMediaControl imediacontrol = null;
        IMediaControl imediaSourcecontrol = null;
        IStreamBufferRecordControl recorder = null;
        public string target = "";
        public string devicename = null;
        string readlast = null;
        // Signal stats
        List<IBDA_SignalStatistics> signalStats = new List<IBDA_SignalStatistics>();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string subKey, uint options, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        private const int KEY_QUERY_VALUE = 0x0001;
        private const int KEY_NOTIFY = 0x0010;
        private const int STANDARD_RIGHTS_READ = 0x00020000;
        private const int STANDARD_RIGHTS_WRITE = 0x00020000;

        private static readonly IntPtr HKEY_CLASSES_ROOT = new IntPtr(unchecked((int)0x80000000));
        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        private static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
        private static readonly IntPtr HKEY_USERS = new IntPtr(unchecked((int)0x80000003));
        private static readonly IntPtr HKEY_PERFORMANCE_DATA = new IntPtr(unchecked((int)0x80000004));
        private static readonly IntPtr HKEY_CURRENT_CONFIG = new IntPtr(unchecked((int)0x80000005));
        private static readonly IntPtr HKEY_DYN_DATA = new IntPtr(unchecked((int)0x80000006));
        bool fullscreen = false;
        public Form1()
        {
            InitializeComponent();
        }


        public void BuildGraph(ITuningSpace tuningSpace)
        {
            this.graphBuilder = (IFilterGraph2)new FilterGraph();
            rot = new DsROTEntry(this.graphBuilder);

            AddNetworkProviderFilter(tuningSpace);
            AddMPEG2DemuxFilter();
            AddAndConnectBDABoardFilters();
            AddTransportStreamFiltersToGraph();
            ConnectNetworkBDAFilters();
            connectasf();

        }





        #region Membres de IDisposable
        public void Dispose()
        {
            StreamReader s = File.OpenText("channel.txt");
            string read = null;
            ArrayList Channels = new ArrayList();
            while ((read = s.ReadLine()) != null)
            {
                string[] strArr = read.ToString().Split('$');
                for (int k = 0; k < strArr.Length; k++)
                {
                    string strTemp = strArr[k];
                    string[] lineArr = strTemp.Split(';');
                    if (lineArr[0].ToString() == "Channel")
                    {
                        if (combo_chnl.Text == lineArr[15].ToString())
                        {
                            using (StreamWriter writer = new StreamWriter("lastchannel.txt"))
                            {
                                writer.WriteLine(lineArr[1].ToString());
                                writer.Close();
                            }
                        }
                    }
                }
            }
            s.Close();
            Decompose();
        }
        #endregion

        private void AddNetworkProviderFilter(ITuningSpace tuningSpace)
        {
            int hr = 0;
            Guid genProviderClsId = new Guid("{B2F3A67C-29DA-4C78-8831-091ED509A475}");
            Guid networkProviderClsId;

            // First test if the Generic Network Provider is available (only on MCE 2005 + Update Rollup 2 and higher)
            if (FilterGraphTools.IsThisComObjectInstalled(genProviderClsId))
            {
                this.networkProvider = FilterGraphTools.AddFilterFromClsid(this.graphBuilder, genProviderClsId, "Generic Network Provider");

                hr = (this.networkProvider as ITuner).put_TuningSpace(tuningSpace);
                return;
            }

            // Get the network type of the requested Tuning Space
            hr = tuningSpace.get__NetworkType(out networkProviderClsId);

            // Get the network type of the requested Tuning Space
            if (networkProviderClsId == typeof(DVBTNetworkProvider).GUID)
            {
                this.networkProvider = FilterGraphTools.AddFilterFromClsid(this.graphBuilder, networkProviderClsId, "DVBT Network Provider");
            }
            else if (networkProviderClsId == typeof(DVBSNetworkProvider).GUID)
            {
                this.networkProvider = FilterGraphTools.AddFilterFromClsid(this.graphBuilder, networkProviderClsId, "DVBS Network Provider");
            }
            else if (networkProviderClsId == typeof(ATSCNetworkProvider).GUID)
            {
                this.networkProvider = FilterGraphTools.AddFilterFromClsid(this.graphBuilder, networkProviderClsId, "ATSC Network Provider");
            }
            else if (networkProviderClsId == typeof(DVBCNetworkProvider).GUID)
            {
                this.networkProvider = FilterGraphTools.AddFilterFromClsid(this.graphBuilder, networkProviderClsId, "DVBC Network Provider");
            }
            else
                // Tuning Space can also describe Analog TV but this application don't support them
                throw new ArgumentException("This application doesn't support this Tuning Space");

            hr = (this.networkProvider as ITuner).put_TuningSpace(tuningSpace);
        }
        private void AddMPEG2DemuxFilter()
        {
            int hr = 0;

            this.mpeg2Demux = (IBaseFilter)new MPEG2Demultiplexer();

            hr = this.graphBuilder.AddFilter(this.mpeg2Demux, "MPEG2 Demultiplexer");
            DsError.ThrowExceptionForHR(hr);



        }

        private void AddAndConnectBDABoardFilters()
        {
            int hr = 0;
            DsDevice[] devices;

            // Enumerate BDA Source filters category and found one that can connect to the network provider

            capBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();

            capBuilder.SetFiltergraph(this.graphBuilder);
            devices = DsDevice.GetDevicesOfCat(FilterCategory.BDASourceFiltersCategory);

            try
            {

                for (int i = 0; i < devices.Length; i++)
                {
                    IBaseFilter tmp;

                    hr = graphBuilder.AddSourceFilterForMoniker(devices[i].Mon, null, devices[i].Name, out tmp);
                    DsError.ThrowExceptionForHR(hr);

                    hr = capBuilder.RenderStream(null, null, this.networkProvider, null, tmp);
                    if (hr == 0)
                    {
                        // Got it !
                        this.tuner = tmp;
                        devicename = devices[i].Name.ToString();
                        break;
                    }
                    else
                    {
                        // Try another...
                        hr = graphBuilder.RemoveFilter(tmp);
                        Marshal.ReleaseComObject(tmp);
                    }
                }

                if (this.tuner == null)
                {
                    MessageBox.Show("Can't find a valid BDA tuner");
                    this.Dispose();
                    this.Close();
                }

                // trying to connect this filter to the MPEG-2 Demux
                hr = capBuilder.RenderStream(null, null, tuner, null, mpeg2Demux);
                if (hr >= 0)
                {
                    // this is a one filter model
                    this.demodulator = null;
                    this.capture = null;
                    return;
                }
                //////////////////////////////////////////////////////////
                else
                {
                    // Then enumerate BDA Receiver Components category to found a filter connecting 
                    // to the tuner and the MPEG2 Demux
                    devices = DsDevice.GetDevicesOfCat(FilterCategory.BDAReceiverComponentsCategory);

                    for (int i = 0; i < devices.Length; i++)
                    {
                        IBaseFilter tmp;

                        hr = graphBuilder.AddSourceFilterForMoniker(devices[i].Mon, null, devices[i].Name, out tmp);
                        DsError.ThrowExceptionForHR(hr);

                        hr = capBuilder.RenderStream(null, null, this.tuner, null, tmp);
                        if (hr == 0)
                        {
                            // Got it !
                            this.capture = tmp;

                            // Connect it to the MPEG-2 Demux
                            hr = capBuilder.RenderStream(null, null, this.capture, null, this.mpeg2Demux);
                            if (hr >= 0)
                            {
                                // This second filter connect both with the tuner and the demux.
                                // This is a capture filter...
                                return;
                            }
                            else
                            {
                                // This second filter connect with the tuner but not with the demux.
                                // This is in fact a demodulator filter. We now must find the true capture filter...

                                this.demodulator = this.capture;
                                this.capture = null;

                                // saving the Demodulator's DevicePath to avoid creating it twice.
                                string demodulatorDevicePath = devices[i].DevicePath;

                                for (int j = 0; i < devices.Length; j++)
                                {
                                    if (devices[j].DevicePath.Equals(demodulatorDevicePath))
                                        continue;

                                    hr = graphBuilder.AddSourceFilterForMoniker(devices[i].Mon, null, devices[i].Name, out tmp);
                                    DsError.ThrowExceptionForHR(hr);

                                    hr = capBuilder.RenderStream(null, null, this.demodulator, null, tmp);
                                    if (hr == 0)
                                    {
                                        // Got it !
                                        this.capture = tmp;

                                        // Connect it to the MPEG-2 Demux
                                        hr = capBuilder.RenderStream(null, null, this.capture, null, this.mpeg2Demux);
                                        if (hr >= 0)
                                        {
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        // Try another...
                                        hr = graphBuilder.RemoveFilter(tmp);
                                        Marshal.ReleaseComObject(tmp);
                                    }
                                } // for j

                                // We have a tuner and a capture/demodulator that don't connect with the demux
                                // and we found no additionals filters to build a working filters chain.
                                throw new ApplicationException("Can't find a valid BDA filter chain");
                            }
                        }
                        else
                        {
                            // Try another...
                            hr = graphBuilder.RemoveFilter(tmp);
                            Marshal.ReleaseComObject(tmp);
                        }
                    } // for i

                    // We have a tuner that connect to the Network Provider BUT not with the demux
                    // and we found no additionals filters to build a working filters chain.
                    //throw new ApplicationException("Can't find a valid BDA filter chain");
                }



            }
            finally
            {

                Marshal.ReleaseComObject(capBuilder);
            }
        }

        public void AddTransportStreamFiltersToGraph()
        {
            int hr = 0;
            DsDevice[] devices;

            // Add two filters needed in a BDA graph
            devices = DsDevice.GetDevicesOfCat(FilterCategory.BDATransportInformationRenderersCategory);
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].Name.Equals("BDA MPEG2 Transport Information Filter"))
                {
                    hr = graphBuilder.AddSourceFilterForMoniker(devices[i].Mon, null, devices[i].Name, out this.bdaTIF);
                    DsError.ThrowExceptionForHR(hr);
                    continue;
                }

                if (devices[i].Name.Equals("MPEG-2 Sections and Tables"))
                {
                    hr = graphBuilder.AddSourceFilterForMoniker(devices[i].Mon, null, devices[i].Name, out this.bdaSecTab);
                    DsError.ThrowExceptionForHR(hr);
                    continue;
                }
            }

        }

        Guid CLSID_GenericNetworkProvider = new Guid("{B2F3A67C-29DA-4C78-8831-091ED509A475}"); //MSNP.ax

        Guid CLSID_AVerMediaBDADVBSTuner = new Guid("{17CCA71B-ECD7-11D0-B908-00A0C9223196}"); //ksproxy.ax 
        Guid CLSID_AVerMediaBDADigitalCapture = new Guid("{17CCA71B-ECD7-11D0-B908-00A0C9223196}"); //ksproxy.ax 
        Guid CLSID_MPEG2SectionsandTables = new Guid("{C666E115-BB62-4027-A113-82D643FE2D99}"); //Mpeg2Data.ax
        Guid CLSID_BDAMPEG2TransportInformationFilter = new Guid("{FC772AB0-0C7F-11D3-8FF2-00A0C9224CF4}"); //psisrndr.ax 
        Guid CLSID_MicrosoftDTVDVDAudioDecoder = new Guid("{E1F1A0B8-BEEE-490D-BA7C-066C40B5E2B9}"); //msmpeg2adec.d11 
        Guid CLSID_MicrosoftDTVDVDVideoDecoder = new Guid("{212690FB-83E5-4526-8FD7-74478B7939CD}"); //msmpeg2vdec.d11 

        IBaseFilter pMicrosoftDTVDVDAudioDecoder = null;
        IBaseFilter pMicrosoftDTVDVDVideoDecoder = null;
        IFileSinkFilter fileSinkFilter;

        private void ConnectNetworkBDAFilters()
        {
            int hr = 0;
            int pinNumber = 0;
            IPin pinOut, pinIn;
            // After the rendering process, our 4 downstream filters must be rendered
            bool bdaTIFRendered = false;
            bool bdaSecTabRendered = false;
            // for each output pins...
            while (true)
            {
                pinOut = DsFindPin.ByDirection(mpeg2Demux, PinDirection.Output, pinNumber);
                // Is the last pin reached ?
                if (pinOut == null)
                    break;

                IEnumMediaTypes enumMediaTypes = null;
                AMMediaType[] mediaTypes = new AMMediaType[1];

                try
                {
                    // Get Pin's MediaType enumerator
                    hr = pinOut.EnumMediaTypes(out enumMediaTypes);
                    DsError.ThrowExceptionForHR(hr);

                    // for each media types...
                    while (enumMediaTypes.Next(mediaTypes.Length, mediaTypes, IntPtr.Zero) == 0)
                    {
                        // Store the majortype and the subtype and free the structure
                        Guid majorType = mediaTypes[0].majorType;
                        Guid subType = mediaTypes[0].subType;
                        DsUtils.FreeAMMediaType(mediaTypes[0]);

                        if (majorType == MediaType.Mpeg2Sections)
                        {
                            if (subType == MediaSubType.Mpeg2Data)
                            {
                                // Is the MPEG-2 Sections and Tables Filter already rendered ?
                                if (!bdaSecTabRendered)
                                {
                                    // Get the first input pin
                                    pinIn = DsFindPin.ByDirection(bdaSecTab, PinDirection.Input, 0);

                                    // A direct connection is enough
                                    hr = graphBuilder.ConnectDirect(pinOut, pinIn, null);
                                    DsError.ThrowExceptionForHR(hr);

                                    // Release the Pin
                                    Marshal.ReleaseComObject(pinIn);
                                    pinIn = null;

                                    // Notify that the MPEG-2 Sections and Tables Filter is connected
                                    bdaSecTabRendered = true;
                                }
                            }
                            // This sample only support DVB-T or DVB-S so only supporting this subtype is enough.
                            // If you want to support ATSC or ISDB, don't forget to handle these network types.
                            else if (subType == MediaSubType.DvbSI)
                            {
                                // Is the BDA MPEG-2 Transport Information Filter already rendered ?
                                if (!bdaTIFRendered)
                                {
                                    // Get the first input pin
                                    pinIn = DsFindPin.ByDirection(bdaTIF, PinDirection.Input, 0);

                                    // A direct connection is enough
                                    hr = graphBuilder.ConnectDirect(pinOut, pinIn, null);
                                    DsError.ThrowExceptionForHR(hr);

                                    // Release the Pin
                                    Marshal.ReleaseComObject(pinIn);
                                    pinIn = null;

                                    // Notify that the BDA MPEG-2 Transport Information Filter is connected
                                    bdaTIFRendered = true;
                                }
                            }
                        }
                    }
                }
                finally
                {
                    // Free COM objects
                    Marshal.ReleaseComObject(enumMediaTypes);
                    enumMediaTypes = null;

                    Marshal.ReleaseComObject(pinOut);
                    pinOut = null;
                }
                // Next pin, please !
                pinNumber++;
            }


        }
        private void connectasf()
        {
            int hr = 0;
            pMicrosoftDTVDVDAudioDecoder = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MicrosoftDTVDVDAudioDecoder));
            hr = graphBuilder.AddFilter(pMicrosoftDTVDVDAudioDecoder, "Microsoft DTV-DVD Audio Decoder");
            checkHR(hr, "Can't add Microsoft DTV-DVD Audio Decoder to graph");

            //connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Audio Decoder
            hr = graphBuilder.ConnectDirect(GetPin(mpeg2Demux, "007"), GetPin(pMicrosoftDTVDVDAudioDecoder, "XForm In"), null);
            checkHR(hr, "Can't connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Audio Decoder");
            WMASFWritter = (IBaseFilter)new WMAsfWriter();
            hr = graphBuilder.AddFilter(WMASFWritter, "WM ASF Writer");
            checkHR(hr, "Can't add WM ASF Writer to graph");
            //set destination filename 
            asfwrittercreate();
            hr = graphBuilder.ConnectDirect(GetPin(pMicrosoftDTVDVDAudioDecoder, "XFrom Out"), GetPin(WMASFWritter, "Audio Input 01"), null);
            checkHR(hr, "Can't connect Microsoft DTV-DVD Audio Decoder and WM ASF Writer");
            //add Microsoft DTV-DVD Video Decoder 
            pMicrosoftDTVDVDVideoDecoder = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MicrosoftDTVDVDVideoDecoder));
            hr = graphBuilder.AddFilter(pMicrosoftDTVDVDVideoDecoder, "Microsoft DTV-DVD Video Decoder");
            checkHR(hr, "Can't add Microsoft DTV-DVD Video Decoder to graph");
            //connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Video Decoder 
            hr = graphBuilder.ConnectDirect(GetPin(mpeg2Demux, "004"), GetPin(pMicrosoftDTVDVDVideoDecoder, "Video Input"), null);


            checkHR(hr, "Can't connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Video Decoder");
            //connect Microsoft DTV-DVD Video Decoder and WM ASF Writer 
            hr = graphBuilder.ConnectDirect(GetPin(pMicrosoftDTVDVDVideoDecoder, "Video Output 1"), GetPin(WMASFWritter, "Video Input 01"), null);
            checkHR(hr, "Can't connect Microsoft DTV-DVD Video Decoder and WM ASF Writer");


        }
        private void connectasf2()
        {
            int hr = 0;
            pMicrosoftDTVDVDAudioDecoder = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MicrosoftDTVDVDAudioDecoder));
            hr = pSourceGraph.AddFilter(pMicrosoftDTVDVDAudioDecoder, "Microsoft DTV-DVD Audio Decoder");
            checkHR(hr, "Can't add Microsoft DTV-DVD Audio Decoder to graph");


            WMASFWritter = (IBaseFilter)new WMAsfWriter();
            hr = graphBuilder.AddFilter(WMASFWritter, "WM ASF Writer");
            checkHR(hr, "Can't add WM ASF Writer to graph");
            //set destination filename 
            asfwrittercreate();

            //add Microsoft DTV-DVD Video Decoder 
            pMicrosoftDTVDVDVideoDecoder = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MicrosoftDTVDVDVideoDecoder));
            hr = graphBuilder.AddFilter(pMicrosoftDTVDVDVideoDecoder, "Microsoft DTV-DVD Video Decoder");
            checkHR(hr, "Can't add Microsoft DTV-DVD Video Decoder to graph");
            //connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Video Decoder 

            hr = capBuilder.RenderStream(null, MediaType.Video, pMicrosoftDTVDVDVideoDecoder, null, WMASFWritter);
            checkHR(hr, "Can't renderstream WM ASF Writer to Video ");
            hr = capBuilder.RenderStream(null, MediaType.Audio, pMicrosoftDTVDVDAudioDecoder, null, WMASFWritter);
            checkHR(hr, "Can't renderstream WM ASF Writer to Audi");
        }
        static IPin GetPin(IBaseFilter filter, string pinname)
        {
            IEnumPins epins;
            int hr = filter.EnumPins(out epins);
            checkHR(hr, "Can't enumerate pins");
            IntPtr fetched = Marshal.AllocCoTaskMem(4);
            IPin[] pins = new IPin[1];
            while (epins.Next(1, pins, fetched) == 0)
            {
                PinInfo pinfo;
                pins[0].QueryPinInfo(out pinfo);
                bool found = (pinfo.name == pinname);
                DsUtils.FreePinInfo(pinfo);
                if (found)
                    return pins[0];
            }
            checkHR(-1, "Pin not found");
            return null;
        }

        private void AddCaptureRenderers()
        {
            bool streamBufferSinkVideoRendered = false;
            bool streamBufferSinkAudioRendered = false;
            bool AddStreamFilters = false;
            IPin pinOut, pinIn;
            int pinNumber = 0;
            int hr = 0;
            if (!AddStreamFilters)
            {
                streamBufferSink = (IBaseFilter)new StreamBufferSink();
                hr = this.graphBuilder.AddFilter(streamBufferSink, "Stream Buffer Sink");
                DsError.ThrowExceptionForHR(hr);
                AddStreamFilters = true;
            }



            if (AddStreamFilters)
            {
                while (true)
                {
                    pinOut = DsFindPin.ByDirection(mpeg2Demux, PinDirection.Output, pinNumber);
                    // Is the last pin reached ?
                    if (pinOut == null)
                        break;

                    IEnumMediaTypes enumMediaTypes = null;
                    AMMediaType[] mediaTypes = new AMMediaType[1];

                    try
                    {
                        // Get Pin's MediaType enumerator
                        hr = pinOut.EnumMediaTypes(out enumMediaTypes);
                        DsError.ThrowExceptionForHR(hr);
                        // for each media types...
                        while (enumMediaTypes.Next(mediaTypes.Length, mediaTypes, IntPtr.Zero) == 0)
                        {
                            // Store the majortype and the subtype and free the structure
                            Guid majorType = mediaTypes[0].majorType;
                            Guid subType = mediaTypes[0].subType;
                            DsUtils.FreeAMMediaType(mediaTypes[0]);
                            if (majorType == MediaType.Audio)
                            {
                                if (subType == MediaSubType.Mpeg2Audio)
                                {
                                    if (!streamBufferSinkAudioRendered)
                                    {
                                        try
                                        {
                                            pinIn = DsFindPin.ByConnectionStatus(streamBufferSink, PinConnectedStatus.Unconnected, 0);
                                            if (pinIn != null)
                                            {
                                                hr = this.graphBuilder.Connect(pinOut, pinIn);
                                                DsError.ThrowExceptionForHR(hr);
                                                streamBufferSinkAudioRendered = true;
                                            }
                                            Marshal.ReleaseComObject(pinIn);
                                            pinIn = null;
                                        }
                                        catch { };
                                    }
                                }
                            }
                            if (majorType == MediaType.Video)
                            {
                                if (subType == MediaSubType.Mpeg2Video)
                                {
                                    if (!streamBufferSinkVideoRendered)
                                    {
                                        try
                                        {
                                            pinIn = DsFindPin.ByConnectionStatus(streamBufferSink, PinConnectedStatus.Unconnected, 0);
                                            if (pinIn != null)
                                            {
                                                hr = this.graphBuilder.Connect(pinOut, pinIn);
                                                DsError.ThrowExceptionForHR(hr);
                                                streamBufferSinkVideoRendered = true;
                                            }
                                            Marshal.ReleaseComObject(pinIn);
                                            pinIn = null;
                                        }
                                        catch
                                        { }
                                    }
                                }
                            }

                        }
                    }
                    finally
                    {
                        // Free COM objects
                        Marshal.ReleaseComObject(enumMediaTypes);
                        enumMediaTypes = null;
                        Marshal.ReleaseComObject(pinOut);
                        pinOut = null;
                    }
                    // Next pin, please !
                    pinNumber++;
                }





            }
        }

        private void AddCapSourceFilter()
        {

            try
            {
                int hr = 0;
                IBaseFilter videodecoder = null;
                IBaseFilter audiodecoder = null;
                IPin pinIn, pinOut;
                pSourceGraph = (IFilterGraph2)new FilterGraph();
                rot = new DsROTEntry(pSourceGraph);



                streamBufferSource = (IBaseFilter)new StreamBufferSource();
                hr = pSourceGraph.AddFilter(streamBufferSource, "Stream Buffer Source");
                DsError.ThrowExceptionForHR(hr);



                hr = ((IStreamBufferSource)streamBufferSource).SetStreamSink((IStreamBufferSink)streamBufferSink);
                DsError.ThrowExceptionForHR(hr);






                this.videoRenderer = (IBaseFilter)new VideoMixingRenderer9();
                ConfigureVMR9InWindowlessMode();
                hr = this.pSourceGraph.AddFilter(this.videoRenderer, "Video Renderer 9");

                this.audioRenderer = (IBaseFilter)new DSoundRender();
                hr = this.pSourceGraph.AddFilter(this.audioRenderer, "DirectSound Renderer");

                audiodecoder = DirectShowLib.Utils.MicrosoftDTVDvFilter.Audio;
                hr = this.pSourceGraph.AddFilter(this.audioRenderer, "Microsoft DTV-DVD Audio Decoder");

                DsError.ThrowExceptionForHR(hr);







                if (videodecoder == null)
                {
                    videodecoder = FilterGraphTools.AddFilterByName
                     (pSourceGraph, FilterCategory.LegacyAmFilterCategory, "Microsoft DTV-DVD Video Decoder");
                }

                //if (audiodecoder == null)
                //{
                //    audiodecoder = FilterGraphTools.AddFilterByName
                //     (pSourceGraph, FilterCategory.LegacyAmFilterCategory, "Microsoft DTV-DVD Audio Decoder");
                //}

                pinOut = DsFindPin.ByName(streamBufferSource, "DVR Out - 2");
                pinIn = DsFindPin.ByDirection(videodecoder, PinDirection.Input, 0);
                pSourceGraph.Connect(pinOut, pinIn);




                pinOut = DsFindPin.ByDirection(videodecoder, PinDirection.Output, 0);
                pinIn = DsFindPin.ByDirection(videoRenderer, PinDirection.Input, 0);
                pSourceGraph.Connect(pinOut, pinIn);



                IEnumPins enumPins = null;
                IPin[] pin = new IPin[1];

                streamBufferSource.EnumPins(out enumPins);
                while (enumPins.Next(pin.Length, pin, IntPtr.Zero) == 0)
                {
                    IPin connectedPin = null;
                    hr = pin[0].ConnectedTo(out connectedPin);
                    if (hr != 0)
                    {
                        hr = graphBuilder.Render(pin[0]);

                    }
                    else
                        Marshal.ReleaseComObject(connectedPin);
                    Marshal.ReleaseComObject(pin[0]);
                }

              

              
            }
            catch (Exception ex)
            {

            }

        }

        private int asfwrittercreate()
        {
            int hr = 0;
            try
            {
                IWMWriterAdvanced pWriterAdvanced = null;

                // I don't understand why we can't just QueryInterface for a IWMWriterAdvanced, but
                // we just can't.  So, we use an IServiceProvider



                fileSinkFilter = (IFileSinkFilter)WMASFWritter;
                fileSinkFilter.SetFileName("c://erkan.wmv", null);

                DsError.ThrowExceptionForHR(hr);


                DirectShowLib.IServiceProvider pProvider = WMASFWritter as DirectShowLib.IServiceProvider;

                if (pProvider != null)
                {
                    object opro;

                    hr = pProvider.QueryService(typeof(IWMWriterAdvanced2).GUID, typeof(IWMWriterAdvanced2).GUID, out opro);
                    WMError.ThrowExceptionForHR(hr);

                    pWriterAdvanced = opro as IWMWriterAdvanced;
                    if (pWriterAdvanced == null)
                    {
                        throw new Exception("Can't get IWMWriterAdvanced");
                    }
                }

                IFileSinkFilter pTmpSink = null;

                // You *must* set an output file name.  Even though it never gets used
                // hr = capBuilder.SetOutputFileName(MediaSubType.Asf, "c:/erkan.wmv", out WMASFWritter, out pTmpSink);
                DsError.ThrowExceptionForHR(hr);


                ConfigProfileFromFile(WMASFWritter, Application.StartupPath+ "+/prf.prx");


                try
                {
                    // Remove all the sinks from the writer
                    RemoveAllSinks(pWriterAdvanced);

                    // Say we are using live data
                    pWriterAdvanced.SetLiveSource(true);

                    // Create a network sink
                    WMUtils.WMCreateWriterNetworkSink(out m_pNetSink);

                    // Configure the network sink
                    m_pNetSink.SetNetworkProtocol(NetProtocol.HTTP);

                    // Configure the network sink
                    m_pNetSink.SetMaximumClients(MaxClients);

                    // Done configuring the network sink, open it
                    int dwPortNum = 8080;

                    m_pNetSink.Open(ref dwPortNum);

                    button1.Text = GetURL();
                    // Add the network sink to the IWMWriterAdvanced
                    pWriterAdvanced.AddSink(m_pNetSink as IWMWriterSink);

                }
                finally
                {
                    if (pWriterAdvanced != null)
                    {
                        Marshal.ReleaseComObject(pWriterAdvanced);
                        pWriterAdvanced = null;
                    }
                }

            }
            catch (Exception ex)
            {

                throw ex;
            }
            return hr;
        }

        public bool ConfigProfileFromFile(IBaseFilter asfWriter, string filename)
        {
            int hr;
            //string profilePath = "test.prx";
            // Set the profile to be used for conversion
            if ((filename != null) && (File.Exists(filename)))
            {
                // Load the profile XML contents
                string profileData;
                using (StreamReader reader = new StreamReader(File.OpenRead(filename)))
                {
                    profileData = reader.ReadToEnd();
                }

                // Create an appropriate IWMProfile from the data
                // Open the profile manager
                IWMProfileManager profileManager;
                IWMProfile wmProfile = null;
                WindowsMediaLib.WMUtils.WMCreateProfileManager(out profileManager);

                // error message: The profile is invalid (0xC00D0BC6)
                // E.g. no <prx> tags
                profileManager.LoadProfileByData(profileData, out wmProfile);


                if (profileManager != null)
                {
                    Marshal.ReleaseComObject(profileManager);
                    profileManager = null;
                }

                // Config only if there is a profile retrieved

                // Set the profile on the writer
                WindowsMediaLib.IConfigAsfWriter configWriter = (WindowsMediaLib.IConfigAsfWriter)asfWriter;
                configWriter.ConfigureFilterUsingProfile(wmProfile);

            }
            return true;
        }

        IMediaEvent mevent = null;
        public void RunCapGraph()
        {
            int hr = 0;
            imediaSourcecontrol = (this.pSourceGraph as IMediaControl);
            try
            {

                mevent = pSourceGraph as IMediaEvent;

                hr = imediaSourcecontrol.Run();
                if (hr != 0)
                    DsError.ThrowExceptionForHR(hr);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Grafik Çalıştırılamadı");
                ex = ex;
            }

        }
        public void StopCapGraph()
        {
            int hr = 0;
            imediaSourcecontrol = (this.pSourceGraph as IMediaControl);
            try
            {
                hr = imediaSourcecontrol.Stop();
                if (hr != 0)
                    DsError.ThrowExceptionForHR(hr);
            }
            catch { }
        }
        public void RunGraph()
        {
            int hr = 0;
            imediacontrol = (this.graphBuilder as IMediaControl);
            try
            {
                hr = imediacontrol.Run();
                if (hr != 0)
                    DsError.ThrowExceptionForHR(hr);
            }
            catch { }
        }
        public void StopGraph()
        {
            int hr = 0;
            imediacontrol = (this.graphBuilder as IMediaControl);
            try
            {
                hr = imediacontrol.Stop();
                if (hr != 0)
                    DsError.ThrowExceptionForHR(hr);
            }
            catch { }
        }
        private void Decompose()
        {
            try
            {
                int hr = 0;
                // Decompose the graph
                hr = (this.graphBuilder as IMediaControl).StopWhenReady();
                hr = (this.graphBuilder as IMediaControl).Stop();
                hr = (this.pSourceGraph as IMediaControl).StopWhenReady();
                hr = (this.pSourceGraph as IMediaControl).Stop();
                RemoveHandlers();
                FilterGraphTools.RemoveAllFilters(this.graphBuilder);
                FilterGraphTools.RemoveAllFilters(this.pSourceGraph);
                Marshal.ReleaseComObject(this.networkProvider); this.networkProvider = null;
                Marshal.ReleaseComObject(this.mpeg2Demux); this.mpeg2Demux = null;
                Marshal.ReleaseComObject(this.tuner); this.tuner = null;
                Marshal.ReleaseComObject(this.bdaTIF); this.bdaTIF = null;
                Marshal.ReleaseComObject(this.vidcapwindow); this.vidcapwindow = null;
                Marshal.ReleaseComObject(this.videoRenderer); this.videoRenderer = null;
                Marshal.ReleaseComObject(this.audioRenderer); this.audioRenderer = null;
                rot.Dispose();
                Marshal.ReleaseComObject(this.graphBuilder); this.graphBuilder = null;
                Marshal.ReleaseComObject(this.pSourceGraph); this.pSourceGraph = null;
            }
            catch (Exception ex)
            {
                ex = ex;
            }

        }
        public static Guid GetPinCategory(IPin pPin)
        {
            Guid guidRet = Guid.Empty;

            // Memory to hold the returned guid 
            int iSize = Marshal.SizeOf(typeof(Guid));
            IntPtr ipOut = Marshal.AllocCoTaskMem(iSize);

            try
            {
                int hr;
                int cbBytes;
                Guid g = PropSetID.Pin;

                // Get an IKsPropertySet from the pin 
                IKsPropertySet pKs = pPin as IKsPropertySet;

                if (pKs != null)
                {
                    // Query for the Category 
                    hr = pKs.Get(g, (int)AMPropertyPin.Category, IntPtr.Zero, 0, ipOut, iSize, out cbBytes);
                    DsError.ThrowExceptionForHR(hr);

                    // Marshal it to the return variable 
                    guidRet = (Guid)Marshal.PtrToStructure(ipOut, typeof(Guid));
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(ipOut);
                ipOut = IntPtr.Zero;
            }

            return guidRet;
        }
        private void SetSinkFile()
        {
            int hr = 0;
            IntPtr _registryHive = HKEY_CURRENT_USER;
            IntPtr registryKey = IntPtr.Zero;

            if (hr != 0)
                MessageBox.Show("Not Stop imediacontrol");

            IStreamBufferSink sink = (IStreamBufferSink)streamBufferSink;
            try
            {
                IStreamBufferConfigure3 config = (IStreamBufferConfigure3)new StreamBufferConfig();
                IStreamBufferInitialize init = config as IStreamBufferInitialize;
                string keyName = string.Format(@"Software\SBERender\{0}", Guid.NewGuid().ToString());
                RegistryKey rk = Registry.CurrentUser.CreateSubKey(keyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
                rk.Close();
                int result = RegOpenKeyEx(_registryHive, keyName, 0, STANDARD_RIGHTS_READ | KEY_QUERY_VALUE | STANDARD_RIGHTS_WRITE, out registryKey);

                hr = init.SetHKEY(registryKey);
                if (hr != 0)
                    MessageBox.Show("Not set the Hkey");

                hr = config.SetDirectory(Path.GetDirectoryName(target));
                hr = config.SetNamespace(null);
                IStreamBufferInitialize init2 = sink as IStreamBufferInitialize;
                hr = init2.SetHKEY(registryKey);
                if (hr != 0)
                    MessageBox.Show("Not set the registryKey");
                hr = sink.LockProfile(Path.Combine(Path.GetDirectoryName(target), string.Format("{0}.sbe", Path.GetFileNameWithoutExtension(target))));
            }
            catch { }
        }
        public bool Recording
        {
            get
            {
                if (recorder != null)
                {
                    bool hasStarted = false;
                    bool hasStopped = false;
                    int prevHr = 0;
                    int hr = recorder.GetRecordingStatus(out prevHr, out hasStarted, out hasStopped);

                    if (hr != 0)
                        MessageBox.Show("Failed to get recording status");

                    return hasStarted && !hasStopped;

                }
                else
                {
                    return false;
                }
            }
        }
        protected void CheckAudio()
        {

            IBasicAudio audio = pSourceGraph as IBasicAudio;
            if (audio != null)
                audio.put_Volume(1);
        }
        private void add_H264_pin()
        {
            IPin OutputPin = null;
            IPin pinOut = null;
            IPin pinIn = null;
            AMMediaType mediaVideo = new AMMediaType();
            mediaVideo.majorType = MediaType.Video;
            mediaVideo.subType = MediaSubType.H264;
            mediaVideo.formatType = FormatType.VideoInfo2;
            mediaVideo.temporalCompression = false;
            mediaVideo.fixedSizeSamples = true;
            ((IMpeg2Demultiplexer)mpeg2Demux).CreateOutputPin(mediaVideo, "H264", out OutputPin);
            IMPEG2PIDMap pidmap2 = (IMPEG2PIDMap)OutputPin;
            int hr = pidmap2.MapPID(1, new int[1] { 0xE0 }, MediaSampleContent.ElementaryStream);

            //IBaseFilter ffdvideodecoder = FilterGraphTools.AddFilterByName
            //         (this.graphBuilder, FilterCategory.LegacyAmFilterCategory, "Microsoft DTV-DVD Video Decoder");
            //IBaseFilter videoRendererfirst = (IBaseFilter)new VideoMixingRenderer9();
            //hr = this.graphBuilder.AddFilter(videoRendererfirst, "Video Renderer 9");
            //pinOut = DsFindPin.ByName(mpeg2Demux, "H264");          
            //pinIn = DsFindPin.ByDirection(ffdvideodecoder, PinDirection.Input, 0);                
            //this.graphBuilder.Connect(pinOut, pinIn);
            //pinOut = DsFindPin.ByDirection(ffdvideodecoder, PinDirection.Output, 0);
            //pinIn = DsFindPin.ByDirection(videoRendererfirst, PinDirection.Input, 0);
            //this.graphBuilder.Connect(pinOut, pinIn);

            // Add MPEG4 Demultiplexor.
            //pMPEG4Demultiplexor = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MPEG4Demultiplexor));
            //hr = pGraph.AddFilter(pMPEG4Demultiplexor, "MPEG4 Demultiplexor");
            //CheckHR(hr, "Can't add MPEG4 Demultiplexor to graph.");

        }
        static void checkHR(int hr, string msg)
        {
            if (hr < 0)
            {
                Console.WriteLine(msg);
                DsError.ThrowExceptionForHR(hr);
            }
        }
        public string GetURL()
        {
            int iSize = 0;

            // Call the function once to get the size
            m_pNetSink.GetHostURL(null, ref iSize);

            StringBuilder sRet = new StringBuilder(iSize, 256);
            m_pNetSink.GetHostURL(sRet, ref iSize);

            // Trim off the trailing null
            return sRet.ToString().Substring(0, iSize - 1);
        }
        private void ConfigureVMR9InWindowlessMode()
        {
            int hr = 0;
            IVMRFilterConfig9 filterConfig = this.videoRenderer as IVMRFilterConfig9;

            // Configure VMR-9 in Windowsless mode
            hr = filterConfig.SetRenderingMode(VMR9Mode.Windowless);
            DsError.ThrowExceptionForHR(hr);

            // With 1 input stream
            hr = filterConfig.SetNumberOfStreams(1);
            DsError.ThrowExceptionForHR(hr);

            IVMRWindowlessControl9 windowlessControl = this.videoRenderer as IVMRWindowlessControl9;

            // The main form is hosting the VMR-9
            hr = windowlessControl.SetVideoClippingWindow(this.panel1.Handle);
            DsError.ThrowExceptionForHR(hr);

            // Keep the aspect-ratio OK
            hr = windowlessControl.SetAspectRatioMode(VMR9AspectRatioMode.None);
            DsError.ThrowExceptionForHR(hr);

            // Init the VMR-9 with default size values
            ResizeMoveHandler(null, null);

            // Add Windows Messages handlers
            AddHandlers();
        }
        private void ConfigureVMR9InWindowlessMode2()
        {
            int hr = 0;
            IVMRFilterConfig9 filterConfig = this.WMASFWritter as IVMRFilterConfig9;

            // Configure VMR-9 in Windowsless mode
            hr = filterConfig.SetRenderingMode(VMR9Mode.Windowless);
            DsError.ThrowExceptionForHR(hr);

            // With 1 input stream
            hr = filterConfig.SetNumberOfStreams(1);
            DsError.ThrowExceptionForHR(hr);

            IVMRWindowlessControl9 windowlessControl = this.WMASFWritter as IVMRWindowlessControl9;

            // The main form is hosting the VMR-9
            hr = windowlessControl.SetVideoClippingWindow(this.panel1.Handle);
            DsError.ThrowExceptionForHR(hr);

            // Keep the aspect-ratio OK
            hr = windowlessControl.SetAspectRatioMode(VMR9AspectRatioMode.None);
            DsError.ThrowExceptionForHR(hr);

            // Init the VMR-9 with default size values
            ResizeMoveHandler(null, null);

            // Add Windows Messages handlers
            AddHandlers();
        }

        /// <param name="asfWriter">IBaseFilter from which to get the IWMWriterAdvanced</param>
        /// 
        #region Member variables

        public const int PortNum = 8080;
        public const int MaxClients = 5;

        private IWMWriterNetworkSink m_pNetSink = null;

        private void RemoveAllSinks(IWMWriterAdvanced pWriterAdvanced)
        {
            IWMWriterSink ppSink;
            int iSinkCount;

            pWriterAdvanced.GetSinkCount(out iSinkCount);

            for (int x = iSinkCount - 1; x >= 0; x--)
            {
                pWriterAdvanced.GetSink(x, out ppSink);

                pWriterAdvanced.RemoveSink(ppSink);
            }
        }
        #endregion

        private void Form1_Load(object sender, EventArgs e)
        {

            wait frm = new wait();
            frm.Show();
            frm.ProgressValue = 0;
            target = System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\capture.dvr-ms";
            frm.ProgressValue = 20;
            Application.DoEvents();
            File.Delete(target);
            Add_TuningSpace();
            frm.ProgressValue = 40;
            Application.DoEvents();
            BuildGraph(this.tuningSpace);
            frm.ProgressValue = 60;
            Application.DoEvents();
            AddCaptureRenderers();
            //AddCaptureRenderers();
            SubmitTuneRequest(this.tuneRequest);
            frm.ProgressValue = 70;
            Application.DoEvents();
            SetSinkFile();
            RunGraph();
            AddCapSourceFilter();

            frm.ProgressValue = 80;
            Application.DoEvents();
            RunCapGraph();
            frm.ProgressValue = 90;
            Application.DoEvents();
            if (!File.Exists("channel.txt"))
            {
                System.IO.StreamWriter file = new System.IO.StreamWriter(@"channel.txt", true);
                file.Close();
            }
            else remove_txt_blanks();
            frm.ProgressValue = 95;
            Application.DoEvents();
            combo_chnl_reset();
            SET_Startup_Channel();
            GetTransPonderList("Transponders");
            cmb_Diseqc.SelectedIndex = 1;
            frm.ProgressValue = 100;
            Application.DoEvents();
            frm.Close();

            DirectShowLib.Utils.FilterGraphTools.SaveGraphFile(graphBuilder, "c://graph2.grf");
        }

        #region


        private void cmb_Diseqc_SelectedIndexChanged(object sender, EventArgs e)
        {
            Twinhan cart = new Twinhan(tuner);
            if (cart.IsTwinhan)
            {
                int diseqcport = cmb_Diseqc.SelectedIndex;
                cart.SendDiseqCommand(diseqcport, 11700000, 11700000, 1, diseqcport, 9750000, 10600000);
                int hr = 0;
                ILocator locator;
                int freq, symbolRate;
                Polarisation sigPol;
                ModulationType mod;
                int onid, tsid, sid;

                hr = this.tuneRequest.get_Locator(out locator);
                hr = locator.get_CarrierFrequency(out freq);
                hr = (locator as IDVBSLocator).get_SignalPolarisation(out sigPol);
                hr = locator.get_SymbolRate(out symbolRate);
                hr = locator.get_Modulation(out mod);
                hr = this.tuneRequest.get_ONID(out onid);
                hr = this.tuneRequest.get_TSID(out tsid);
                hr = this.tuneRequest.get_SID(out sid);
                Change_Channel(freq + 1, symbolRate, sigPol, onid, tsid, sid, mod);
            }
        }
        private void GetTransPonderList(string szDrive)
        {
            string currentpath = System.IO.Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar + szDrive;
            try
            {
                string[] dirs = Directory.GetFiles(@currentpath, "*.ini");
                string sOnlyFilename;
                foreach (string dir in dirs)
                {
                    sOnlyFilename = System.IO.Path.GetFileName(dir);
                    cmb_Sat.Items.Add(sOnlyFilename);
                }
                if (cmb_Sat.Items.Count > 0)
                    cmb_Sat.SelectedIndex = 0;
            }
            catch { }

        }
        private void cmb_Sat_SelectedIndexChanged(object sender, EventArgs e)
        {
            string szDrive = "Transponders";
            cmb_Transponder.Items.Clear();
            string currentpath = System.IO.Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar + szDrive;
            int i = 0;
            string inidata = null;
            string[] pathini = Directory.GetFiles(@currentpath, cmb_Sat.Text);
            while (inidata != "")
            {
                i++;
                inidata = FilterGraphTools.GetINIValue(pathini[0].ToString(), "DVB", i.ToString(), "");
                if (inidata != "")
                    cmb_Transponder.Items.Add(inidata);
            }
            if (cmb_Transponder.Items.Count > 0)
                cmb_Transponder.SelectedIndex = 0;

        }
        public void Add_TuningSpace()
        {
            int hr = 0;
            //REGISTER_FILTERGRAPH
            tuningSpace = (IDVBSTuningSpace)new DVBSTuningSpace();
            hr = tuningSpace.put_UniqueName("DVBS TuningSpace");
            hr = tuningSpace.put_FriendlyName("DVBS TuningSpace");
            hr = tuningSpace.put__NetworkType(typeof(DVBSNetworkProvider).GUID);
            hr = tuningSpace.put_SystemType(DVBSystemType.Satellite);
            hr = tuningSpace.put_SpectralInversion(SpectralInversion.Automatic);
            hr = tuningSpace.put_LowOscillator(9750000);
            hr = tuningSpace.put_HighOscillator(10600000);
            hr = tuningSpace.put_LNBSwitch(11700000);
            hr = tuningSpace.put_NetworkID(-1);
            ITuneRequest tr = null;
            hr = tuningSpace.CreateTuneRequest(out tr);
            tuneRequest = (IDVBTuneRequest)tr;
            hr = tuneRequest.put_ONID(-1);
            hr = tuneRequest.put_TSID(-1);
            hr = tuneRequest.put_SID(-1);

            IDVBSLocator locator = (IDVBSLocator)new DVBSLocator();
            locator.put_CarrierFrequency(-1);
            locator.put_SymbolRate(-1);
            locator.put_SignalPolarisation(Polarisation.NotSet);
            locator.put_InnerFEC(FECMethod.MethodNotSet);
            locator.put_InnerFECRate(BinaryConvolutionCodeRate.RateNotSet);
            locator.put_OuterFEC(FECMethod.MethodNotSet);
            locator.put_OuterFECRate(BinaryConvolutionCodeRate.RateNotSet);
            locator.put_Modulation(ModulationType.ModQpsk);
            locator.put_Azimuth(0);
            locator.put_Elevation(0);
            locator.put_OrbitalPosition(0);
            hr = tr.put_Locator(locator as ILocator);
        }
        public void SubmitTuneRequest(ITuneRequest tuneRequest)
        {
            if (tuner != null)
            {
                int hr = 0;
                hr = (this.networkProvider as ITuner).put_TuneRequest(tuneRequest);
                DsError.ThrowExceptionForHR(hr);
            }
        }
        private void RemoveHandlers()
        {
            // Remove Windows Messages handlers
            this.Paint -= new PaintEventHandler(PaintHandler); // for WM_PAINT
            this.Resize -= new EventHandler(ResizeMoveHandler); // for WM_SIZE
            this.Move -= new EventHandler(ResizeMoveHandler); // for WM_MOVE
            SystemEvents.DisplaySettingsChanged -= new EventHandler(DisplayChangedHandler); // for WM_DISPLAYCHANGE
        }
        private void ResizeMoveHandler(object sender, EventArgs e)
        {
            if (this.videoRenderer != null)
            {
                panel1.Width = this.Width;
                panel1.Height = this.Height;
                Rectangle r = new Rectangle(0, 0, this.panel1.Width, this.panel1.Height);
                int hr = (this.videoRenderer as IVMRWindowlessControl9).SetVideoPosition(null, DsRect.FromRectangle(r));
                Graphics g = this.CreateGraphics();
                g.Dispose();

            }

        }
        private void PaintHandler(object sender, PaintEventArgs e)
        {
            if (this.videoRenderer != null)
            {
                IntPtr hdc = e.Graphics.GetHdc();
                try
                {
                    int hr = (this.videoRenderer as IVMRWindowlessControl9).RepaintVideo(this.panel1.Handle, hdc);
                }
                catch { }
                e.Graphics.ReleaseHdc(hdc);
            }
        }

        private void DisplayChangedHandler(object sender, EventArgs e)
        {
            if (this.videoRenderer != null)
            {
                int hr = (this.videoRenderer as IVMRWindowlessControl9).DisplayModeChanged();
            }
        }
        private void createrecorder()
        {
            sink = (IStreamBufferSink)streamBufferSink;
            object objRecorder;

            int hr = sink.CreateRecorder(target, RecordingType.Content, out objRecorder);
            if (hr != 0)
                MessageBox.Show("Not CreateRecorder");
            recorder = objRecorder as IStreamBufferRecordControl;
        }
        private void btn_capture_Click(object sender, EventArgs e)
        {
            int hr = 0;
            sink = (IStreamBufferSink)streamBufferSink;
            FindSignalStats();
            if (btn_capture.Text == "Capture")
            {
                if (ChannelLocked)
                {
                    if (!Recording)
                    {
                        try
                        {
                            target = DateTime.Now.Day.ToString() + "_" +
                                     DateTime.Now.Hour.ToString() + "_" +
                                     DateTime.Now.Minute.ToString() + "_" +
                                     DateTime.Now.Second.ToString() + "_cap.dvr-ms";
                            createrecorder();
                            long start = 0;
                            hr = recorder.Start(ref start);
                            if (hr == 0)
                            {
                                btn_capture.BackColor = Color.Red;
                                btn_capture.Text = "Stop";
                            }
                        }
                        catch { }
                    }
                }
            }
            else
            {
                if (ChannelLocked)
                {
                    if (Recording)
                    {
                        try
                        {
                            hr = recorder.Stop(0);
                            if (hr != 0)
                                File.Delete(target);
                        }
                        catch { }
                    }
                    btn_capture.BackColor = Color.Transparent;
                    btn_capture.Text = "Capture";
                }
            }
        }
        private void AddHandlers()
        {
            // Add Windows Messages handlers
            this.Paint += new PaintEventHandler(PaintHandler); // for WM_PAINT
            this.Resize += new EventHandler(ResizeMoveHandler); // for WM_SIZE
            this.Move += new EventHandler(ResizeMoveHandler); // for WM_MOVE
            SystemEvents.DisplaySettingsChanged += new EventHandler(DisplayChangedHandler); // for WM_DISPLAYCHANGE
        }
        private void combo_chnl_reset()
        {
            combo_chnl.Items.Clear();
            combo_chnl.Text = "";
            text_chnl_name.Text = "";
            Application.DoEvents();
            StreamReader s = File.OpenText("channel.txt");
            string read = null;
            while ((read = s.ReadLine()) != null)
            {
                string[] strArr = read.ToString().Split('$');
                for (int k = 0; k < strArr.Length; k++)
                {
                    string strTemp = strArr[k];
                    string[] lineArr = strTemp.Split(';');

                    if (lineArr[0].ToString() == "Channel")
                    {
                        combo_chnl.Items.Add(lineArr[15].ToString());
                    }
                }
            }
            s.Close();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }
        private void SET_Startup_Channel()
        {
            if (File.Exists("lastchannel.txt"))
            {
                StreamReader slast = File.OpenText("lastchannel.txt");
                readlast = null;
                readlast = slast.ReadLine();
                slast.Close();
            }
            else
            {
                readlast = "1";
            }
            SET_Channel(readlast);
        }
        private void SET_Channel(string channumber)
        {
            StreamReader s = null;
            s = File.OpenText("channel.txt");
            string read = null;
            while ((read = s.ReadLine()) != null)
            {
                string[] strArr = read.ToString().Split('$');
                for (int k = 0; k < strArr.Length; k++)
                {
                    string strTemp = strArr[k];
                    string[] lineArr = strTemp.Split(';');

                    if (lineArr[0].ToString() == "Channel")
                    {
                        if (Convert.ToInt32(lineArr[1].ToString()) == Convert.ToInt32(channumber))
                        {
                            text_chnl_name.Text = lineArr[15].ToString();
                            combo_chnl.SelectedIndex = combo_chnl.FindStringExact((lineArr[15]));
                        }
                    }
                }
                combo_chnl.Enabled = true;
            } s.Close();
        }
        private string GET_Channel()
        {
            StreamReader s = null;
            string channumber = null;
            s = File.OpenText("channel.txt");
            string read = null;
            while ((read = s.ReadLine()) != null)
            {
                string[] strArr = read.ToString().Split('$');
                for (int k = 0; k < strArr.Length; k++)
                {
                    string strTemp = strArr[k];
                    string[] lineArr = strTemp.Split(';');

                    if (lineArr[0].ToString() == "Channel")
                    {
                        if (lineArr[15].ToString() == combo_chnl.Text)
                        {
                            channumber = lineArr[1].ToString();
                        }
                    }
                }
                combo_chnl.Enabled = true;
            } s.Close();
            return channumber;
        }
        private void combo_chnl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (checkBox_info.Checked) ;
            checkBox_info.Checked = false;
            combo_chnl.Enabled = false;
            int freq = 0; int symbolRate = 0; Polarisation strPol = Polarisation.NotSet; int onid = 0; int tsid = 0; int sid = 0;
            string chnl = combo_chnl.SelectedItem.ToString();
            StreamReader s = File.OpenText("channel.txt");
            string read = null;
            while ((read = s.ReadLine()) != null)
            {
                string[] strArr = read.ToString().Split('$');
                for (int k = 0; k < strArr.Length; k++)
                {
                    string strTemp = strArr[k];
                    string[] lineArr = strTemp.Split(';');

                    if (lineArr[0].ToString() == "Channel")
                    {
                        if (chnl == lineArr[15].ToString())
                        {
                            text_chnl_name.Text = lineArr[15];
                            freq = Convert.ToInt32(lineArr[3]);
                            symbolRate = Convert.ToInt32(lineArr[5]);
                            strPol = (Polarisation)Enum.Parse(typeof(Polarisation), lineArr[7].ToString());
                            onid = Convert.ToInt32(lineArr[9]);
                            tsid = Convert.ToInt32(lineArr[11]);
                            sid = Convert.ToInt32(lineArr[13]);
                        }
                    }
                }
            }
            s.Close();
            Change_Channel(freq, symbolRate, strPol, onid, tsid, sid, ModulationType.ModOqpsk);
            combo_chnl.Enabled = true;
        }
        private void set_chnl_Click(object sender, EventArgs e)
        {
            set_chnl.Enabled = false;
            if (text_chnl_name.Text != "")
            {
                Add_Current_Channel(text_chnl_name.Text);
                combo_chnl_reset();
                SET_Startup_Channel();
            }
            else
            {
                MessageBox.Show("Please write a channel name!");
            }
            set_chnl.Enabled = true;
        }
        private void EXIT_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            this.Dispose();
            this.Close();
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            int hr = 0;
            ILocator locator;
            int freq, symbolRate;
            Polarisation sigPol;
            int onid, tsid, sid;

            FindSignalStats();
            progress_strenght.Value = this.SignalStrength;
            progress_quality.Value = this.SignalQuality;
            lbl_strenght.Text = this.SignalStrength.ToString();
            lbl_quality.Text = this.SignalQuality.ToString();
            Lbl_device_name.Text = this.devicename;

            hr = this.tuneRequest.get_Locator(out locator);
            hr = locator.get_CarrierFrequency(out freq);
            hr = (locator as IDVBSLocator).get_SignalPolarisation(out sigPol);
            hr = locator.get_SymbolRate(out symbolRate);
            hr = this.tuneRequest.get_ONID(out onid);
            hr = this.tuneRequest.get_TSID(out tsid);
            hr = this.tuneRequest.get_SID(out sid);
            label1.Text = (freq.ToString());
            label2.Text = (sigPol.ToString());
            label3.Text = (symbolRate.ToString());
            label4.Text = (onid.ToString());
            label5.Text = (tsid.ToString());
            label6.Text = (sid.ToString());

        }
        private void checkBox_info_CheckedChanged(object sender, EventArgs e)
        {

            if (checkBox_info.Checked)
            {
                timer1.Enabled = true;
                panel2.Visible = true;
                panel1.SendToBack();
                panel2.BringToFront();
            }
            else
            {
                timer1.Enabled = false;
                panel2.Visible = false;
                panel2.SendToBack();
                panel1.BringToFront();
            }
        }
        private void remove_txt_blanks()
        {
            var tempFileName = Path.GetTempFileName();
            try
            {
                using (var streamReader = new StreamReader("channel.txt"))
                using (var streamWriter = new StreamWriter(tempFileName))
                {
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        if (line != "")
                            streamWriter.WriteLine(line);
                    }
                }
                File.Copy(tempFileName, "channel.txt", true);
            }
            finally
            {
                File.Delete(tempFileName);
            }
        }
        private void remove_channel_Click(object sender, EventArgs e)
        {
            string chnl = "";
            int i = 0;
            int deleteline = 0;
            if (text_chnl_name.Text.ToString() != "")
            {
                chnl = text_chnl_name.Text.ToString();
                StreamReader s = File.OpenText("channel.txt");
                string read = null;
                while ((read = s.ReadLine()) != null)
                {
                    i++;
                    string[] strArr = read.ToString().Split('$');
                    for (int k = 0; k < strArr.Length; k++)
                    {
                        string strTemp = strArr[k];
                        string[] lineArr = strTemp.Split(';');
                        if (lineArr[0].ToString() == "Channel")
                        {
                            if (chnl == lineArr[15].ToString())
                            {
                                deleteline = i;
                            }
                        }
                    }
                } s.Close();
                var tempFileName = Path.GetTempFileName();
                i = 0;
                try
                {
                    using (var streamReader = new StreamReader("channel.txt"))
                    using (var streamWriter = new StreamWriter(tempFileName))
                    {
                        string line;
                        while ((line = streamReader.ReadLine()) != null)
                        {
                            i++;
                            if (i != deleteline)
                                streamWriter.WriteLine(line);
                        }
                    }

                    File.Copy(tempFileName, "channel.txt", true);
                }
                finally
                {
                    File.Delete(tempFileName);
                }
                combo_chnl_reset();
                SET_Startup_Channel();
            }
        }
        public bool Change_Channel(int freq, int symbolRate, Polarisation strPol, int onid, int tsid, int sid, ModulationType mod)
        {
            int hr = 0;
            ILocator locator = null;
            hr = this.tuneRequest.get_Locator(out locator);
            hr = locator.put_CarrierFrequency(freq);

            hr = (locator as IDVBSLocator).put_SignalPolarisation(strPol);
            hr = locator.put_SymbolRate(symbolRate);

            hr = this.tuneRequest.put_Locator(locator);
            Marshal.ReleaseComObject(locator);

            hr = this.tuneRequest.put_ONID(onid);
            hr = this.tuneRequest.put_TSID(tsid);
            hr = this.tuneRequest.put_SID(sid);
            SubmitTuneRequest(this.tuneRequest);
            return true;
        }
        public bool Add_Current_Channel(string chnlname)
        {
            if (!channel_list_status(chnlname))
            {
                int hr, lastchannel = 0;
                ILocator locator = null;
                int freq = 0; int symbolRate = 0;
                Polarisation sigPol = 0;
                int onid = 0, tsid = 0, sid = 0;

                hr = this.tuneRequest.get_Locator(out locator);
                hr = locator.get_CarrierFrequency(out freq);
                hr = (locator as IDVBSLocator).get_SignalPolarisation(out sigPol);
                hr = locator.get_SymbolRate(out symbolRate);
                hr = this.tuneRequest.get_ONID(out onid);
                hr = this.tuneRequest.get_TSID(out tsid);
                hr = this.tuneRequest.get_SID(out sid);

                StreamReader s = File.OpenText("channel.txt");
                string read = null;
                ArrayList Channels = new ArrayList();
                while ((read = s.ReadLine()) != null)
                {
                    string[] strArr = read.ToString().Split('$');
                    for (int k = 0; k < strArr.Length; k++)
                    {
                        string strTemp = strArr[k];
                        string[] lineArr = strTemp.Split(';');

                        if (lineArr[0].ToString() == "Channel")
                        {
                            Channels.Add(lineArr[1].ToString());
                        }
                    }
                }
                s.Close();
                if (Channels.Count > 0)
                {
                    lastchannel = Convert.ToInt32((Channels[Channels.Count - 1].ToString()));
                }
                using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"channel.txt", true))
                {
                    file.WriteLine("$Channel" + ";" + (lastchannel + 1).ToString() + ";" +
                                   "freq" + ";" + freq.ToString() + ";" +
                                   "symbolRate" + ";" + symbolRate.ToString() + ";" +
                                   "sigPol" + ";" + sigPol.ToString() + ";" +
                                   "onid" + ";" + onid.ToString() + ";" +
                                   "tsid" + ";" + tsid.ToString() + ";" +
                                   "sid" + ";" + sid.ToString() + ";" +
                                   "INFO" + ";" + chnlname + ";");
                    file.Close();
                }
            }
            return true;
        }
        public bool channel_list_status(string chnlname)
        {
            StreamReader s = File.OpenText("channel.txt");
            string read = null;
            bool status = false;
            ArrayList Channels = new ArrayList();
            while ((read = s.ReadLine()) != null)
            {
                string[] strArr = read.ToString().Split('$');
                for (int k = 0; k < strArr.Length; k++)
                {
                    string strTemp = strArr[k];
                    string[] lineArr = strTemp.Split(';');
                    if (lineArr[0].ToString() == "Channel")
                    {
                        if (chnlname == lineArr[15].ToString())
                        {
                            status = true;
                        }
                    }
                }
            }
            s.Close();
            return status;
        }
        public bool Add_Scan_Channel(string chnlname, int freq, int symbolRate, string strPol, int onid, int tsid, int sid, ModulationType mod)
        {
            if (!channel_list_status(chnlname))
            {
                int lastchannel = 0;
                StreamReader s = File.OpenText("channel.txt");
                string read = null;
                ArrayList Channels = new ArrayList();
                while ((read = s.ReadLine()) != null)
                {
                    string[] strArr = read.ToString().Split('$');
                    for (int k = 0; k < strArr.Length; k++)
                    {
                        string strTemp = strArr[k];
                        string[] lineArr = strTemp.Split(';');

                        if (lineArr[0].ToString() == "Channel")
                        {
                            Channels.Add(lineArr[1].ToString());
                        }
                    }
                }
                s.Close();
                if (Channels.Count > 0)
                {
                    lastchannel = Convert.ToInt32((Channels[Channels.Count - 1].ToString()));
                }

                using (System.IO.StreamWriter file = new System.IO.StreamWriter(@"channel.txt", true))
                {
                    file.WriteLine("$Channel" + ";" + (lastchannel + 1).ToString() + ";" +
                                   "freq" + ";" + freq.ToString() + ";" +
                                   "symbolRate" + ";" + symbolRate.ToString() + ";" +
                                   "sigPol" + ";" + strPol.ToString() + ";" +
                                   "onid" + ";" + onid.ToString() + ";" +
                                   "tsid" + ";" + tsid.ToString() + ";" +
                                   "sid" + ";" + sid.ToString() + ";" +
                                   "INFO" + ";" + chnlname + ";");
                    file.Close();
                }
            } return true;
        }
        private void set_freq_Click(object sender, EventArgs e)
        {

            Form2 frm2 = new Form2();
            int hr = 0;
            ILocator locator;
            int freq, symbolRate;
            Polarisation sigPol;
            int onid, tsid, sid;

            hr = this.tuneRequest.get_Locator(out locator);
            hr = locator.get_CarrierFrequency(out freq);
            hr = (locator as IDVBSLocator).get_SignalPolarisation(out sigPol);
            hr = locator.get_SymbolRate(out symbolRate);
            hr = this.tuneRequest.get_ONID(out onid);
            hr = this.tuneRequest.get_TSID(out tsid);
            hr = this.tuneRequest.get_SID(out sid);

            frm2.stextCarrierFreq = freq.ToString();
            frm2.scomboSigPol = sigPol.ToString();
            frm2.stextSymbolRate = symbolRate.ToString();
            frm2.stextONID = onid.ToString();
            frm2.stextTSID = tsid.ToString();
            frm2.stextSID = sid.ToString();
            if (frm2.ShowDialog() == DialogResult.OK)
            {
                int stextCarrierFreq = Convert.ToInt32(frm2.stextCarrierFreq);
                Polarisation scomboSigPol = (Polarisation)Enum.Parse(typeof(Polarisation), frm2.scomboSigPol);
                int stextSymbolRate = Convert.ToInt32(frm2.stextSymbolRate);
                int stextONID = Convert.ToInt32(frm2.stextONID);
                int stextTSID = Convert.ToInt32(frm2.stextTSID);
                int stextSID = Convert.ToInt32(frm2.stextSID);
                Change_Channel(stextCarrierFreq, stextSymbolRate, scomboSigPol, stextONID, stextTSID, stextSID, ModulationType.ModOqpsk);
            }
            set_freq.Enabled = true;
        }
        public const int MAX_NODES = 33;
        public void FindSignalStats()
        {
            int hr = 0;
            IBDA_Topology topology = (IBDA_Topology)tuner;
            int nodeTypeCount = 0;
            int[] nodeTypes = new int[MAX_NODES];
            Guid[] guidInterfaces = new Guid[MAX_NODES];
            hr = topology.GetNodeTypes(out nodeTypeCount, MAX_NODES - 1, nodeTypes);
            DsError.ThrowExceptionForHR(hr);

            for (int i = 0; i < nodeTypeCount; i++)
            {
                object objectNode;
                int numberOfInterfaces = MAX_NODES - 1;
                hr = topology.GetNodeInterfaces(nodeTypes[i], out numberOfInterfaces, MAX_NODES - 1, guidInterfaces);
                DsError.ThrowExceptionForHR(hr);
                hr = topology.GetControlNode(0, 1, nodeTypes[i], out objectNode);
                DsError.ThrowExceptionForHR(hr);

                for (int j = 0; j < numberOfInterfaces; j++)
                {
                    if (guidInterfaces[j] == typeof(IBDA_SignalStatistics).GUID)
                    {
                        signalStats.Add((IBDA_SignalStatistics)objectNode);
                    }
                }
            }

        }
        public bool ChannelLocked
        {
            get
            {
                foreach (IBDA_SignalStatistics signal in signalStats)
                {
                    bool isLocked = false;
                    signal.get_SignalLocked(out isLocked);
                    if (isLocked)
                        return isLocked;
                }
                return false;
            }
        }
        public int SignalQuality
        {
            get
            {
                foreach (IBDA_SignalStatistics signal in signalStats)
                {
                    int quality = 0;
                    signal.get_SignalQuality(out quality);
                    if (quality != 0)
                        return quality;
                }
                return 0;
            }
        }
        public int SignalStrength
        {
            get
            {
                foreach (IBDA_SignalStatistics signal in signalStats)
                {
                    int strength = 0;
                    signal.get_SignalStrength(out strength);
                    if (strength != 0)
                        return strength;
                }
                return 0;
            }
        }
        public string Scan_Transponder(bool allTransponderInfo, int freq, int symbolRate, Polarisation strPol, ModulationType mod)
        {
            Change_Channel(freq, symbolRate, strPol, -1, -1, -1, ModulationType.ModOqpsk);
            string result = "";
            string scannedchannels = "";
            FindSignalStats();
            if (!this.ChannelLocked || this.SignalQuality <= 0)
                result = "No Channel";
            else
            {
                progress_strenght.Value = this.SignalStrength;
                progress_quality.Value = this.SignalQuality;
                lbl_quality.Text = this.SignalQuality.ToString();
                lbl_strenght.Text = this.SignalStrength.ToString();
                IMpeg2Data mpeg2Data = this.bdaSecTab as IMpeg2Data;
                int originalNetworkId = -1;
                Hashtable serviceNameByServiceId = new Hashtable();

                PSISection[] psiSdts = PSISection.GetPSITable((int)PIDS.SDT, (int)TABLE_IDS.SDT_ACTUAL, mpeg2Data);
                PSISection[] psis = PSISection.GetPSITable((int)PIDS.PAT, (int)TABLE_IDS.PAT, mpeg2Data);
                for (int i = 0; i < psiSdts.Length; i++)
                {
                    PSISection psiSdt = psiSdts[i];
                    if (psiSdt != null && psiSdt is PSISDT)
                    {
                        PSISDT sdt = (PSISDT)psiSdt;
                        result += "PSI Table " + i + "/" + psiSdts.Length + "\r\n";
                        result += sdt.ToString();
                        originalNetworkId = (int)sdt.OriginalNetworkId;
                        foreach (PSISDT.Data service in sdt.Services)
                        {
                            serviceNameByServiceId[service.ServiceId] = service.GetServiceName();
                        }
                    }
                }

                for (int i = 0; i < psis.Length; i++)
                {
                    PSISection psi = psis[i];
                    if (psi != null && psi is PSIPAT)
                    {
                        PSIPAT pat = (PSIPAT)psi;
                        result += "PSI Table " + i + "/" + psis.Length + "\r\n";
                        result += pat.ToString();
                        int ONID = originalNetworkId;
                        int TSID = pat.TransportStreamId;
                        foreach (PSIPAT.Data program in pat.ProgramIds)
                        {
                            if (!program.IsNetworkPID)
                            {
                                int SID = program.ProgramNumber;
                                string NAME = (string)serviceNameByServiceId[program.ProgramNumber];
                                int PID = program.Pid;
                                try
                                {
                                    if (Add_Scan_Channel(NAME.ToString(), freq, symbolRate, strPol.ToString(), ONID, TSID, SID, mod))
                                    {
                                        scannedchannels += NAME.ToString() + "\r\n";
                                    }
                                }
                                catch { }

                            }
                        }
                    }
                }
            }

            using (StreamWriter writer = new StreamWriter("data.txt"))
            {
                writer.WriteLine(DateTime.Now + " --- New Data:");
                writer.WriteLine(result);
                writer.WriteLine(DateTime.Now + " --- END New Data:");
                writer.WriteLine("");
                writer.Close();
            }
            if (scannedchannels != "")
            {
                MessageBox.Show("Channels:\r\n" + scannedchannels + "Added to List");
            }
            else
            {
                MessageBox.Show("Not Found Any Channel!");
            }
            return result;
        }
        private void btn_scn_transponder_Click(object sender, EventArgs e)
        {
            int freq, symbolRate;
            Polarisation sigPol = Polarisation.NotSet;
            ModulationType modtype = ModulationType.ModNotSet;
            string strTemp = cmb_Transponder.Text;
            if (strTemp != "")
            {
                string[] lineArr = strTemp.Split(',');
                freq = Convert.ToInt32(lineArr[0].ToString()) * 1000;
                symbolRate = Convert.ToInt32(lineArr[2].ToString());
                try
                {

                    if (lineArr[1].ToString() == "H")
                    {
                        sigPol = Polarisation.LinearH;
                    }
                    else if (lineArr[1].ToString() == "V")
                    {
                        sigPol = Polarisation.LinearV;
                    }
                    else if (lineArr[1].ToString() == "R")
                    {
                        sigPol = Polarisation.CircularR;
                    }


                    if (lineArr[5].ToString() == "QPSK")
                    {
                        modtype = ModulationType.ModQpsk;
                    }
                    else if (lineArr[5].ToString() == "8PSK")
                    {
                        modtype = ModulationType.Mod8Psk;
                    }
                    else
                    {
                        modtype = ModulationType.ModQpsk;
                    }
                }
                catch { }
                Scan_Transponder(false, freq, symbolRate, sigPol, modtype);
                string channumber = GET_Channel();
                combo_chnl_reset();
                SET_Channel(channumber);
            }
        }
        private void panel1_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!fullscreen)
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    this.ControlBox = false;
                    this.MaximizeBox = false;
                    this.MinimizeBox = false;
                    this.FormBorderStyle = FormBorderStyle.None;
                    panel1.BringToFront();
                    if (checkBox_info.Checked)
                    { panel2.BringToFront(); }
                    panel1.Location = new System.Drawing.Point(0, 0);
                    this.WindowState = FormWindowState.Maximized;
                    fullscreen = true;
                }
            }
            else
            {
                if (this.WindowState == FormWindowState.Maximized)
                {
                    this.ControlBox = true;
                    this.MaximizeBox = true;
                    this.MinimizeBox = true;
                    panel1.SendToBack();
                    if (!checkBox_info.Checked)
                    { panel2.SendToBack(); }
                    this.FormBorderStyle = FormBorderStyle.Sizable;
                    panel1.Location = new System.Drawing.Point(0, 53);
                    this.WindowState = FormWindowState.Normal;
                    fullscreen = false;
                }
            }
            panel1.Focus();
            panel1.Invalidate();
        }
        #endregion

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Decompose();
        }

        private void button1_Click(object sender, EventArgs e)
        {


        }
    }
}
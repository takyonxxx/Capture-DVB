using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DirectShowLib;
using System.Runtime.InteropServices;

namespace Turkay_DTV
{
    class Class1
    {
        static void checkHR(int hr, string msg)
        {
            if (hr < 0)
            {
                Console.WriteLine(msg);
                DsError.ThrowExceptionForHR(hr);
            }
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
                if (found) return pins[0];
            }
            checkHR(-1, "Pin not found");
            return null;
        }
        public static void Run(string[] args)
        {
            try
            {
                IGraphBuilder graph = (IGraphBuilder)new FilterGraph();
                Console.WriteLine("Building graph...");
                BuildGraph(graph, "(null)"); 
                Console.WriteLine("Running...");
                IMediaControl mediaControl = (IMediaControl)graph;
                IMediaEvent mediaEvent = (IMediaEvent)graph;
                int hr = mediaControl.Run();
                checkHR(hr, "Can't run the graph");
                bool stop = false;
                while (!stop)
                {
                    System.Threading.Thread.Sleep(500);
                    Console.Write(".");
                    EventCode ev;
                    IntPtr p1, p2;
                    System.Windows.Forms.Application.DoEvents();
                    while (mediaEvent.GetEvent(out ev, out p1, out p2, 0) == 0)
                    {
                        if (ev == EventCode.Complete || ev == EventCode.UserAbort)
                        {
                            Console.WriteLine("Done!");
                            stop = true;
                        }
                        else if (ev == EventCode.ErrorAbort)
                        {
                            Console.WriteLine("An error occured: HRESULT=(0:X)", p1);
                            mediaControl.Stop();
                            stop = true;
                        }
                        mediaEvent.FreeEventParams(ev, p1, p2);
                    }
                }
            }
            catch (COMException ex)
            {

                Console.WriteLine("COM error: " + ex.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }
        static void BuildGraph(IGraphBuilder pGraph, string dstFile1)
        { 

            int hr = 0; 
            //graph builder
            ICaptureGraphBuilder2 pBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            hr = pBuilder.SetFiltergraph(pGraph); checkHR(hr, "Can't SetFiltergraph"); 
            Guid CLSID_GenericNetworkProvider = new Guid("{B2F3A67C-29DA-4C78-8831-091ED509A475}"); //MSNP.ax
                                                          
            Guid CLSID_AVerMediaBDADVBSTuner = new Guid("{17CCA71B-ECD7-11D0-B908-00A0C9223196}"); //ksproxy.ax 
            Guid CLSID_AVerMediaBDADigitalCapture = new Guid("{17CCA71B-ECD7-11D0-B908-00A0C9223196}"); //ksproxy.ax 
            Guid CLSID_MPEG2SectionsandTables = new Guid("{C666E115-BB62-4027-A113-82D643FE2D99}"); //Mpeg2Data.ax
            Guid CLSID_BDAMPEG2TransportInformationFilter = new Guid("{FC772AB0-0C7F-11D3-8FF2-00A0C9224CF4}"); //psisrndr.ax 
            Guid CLSID_MicrosoftDTVDVDAudioDecoder = new Guid("{E1F1A0B8-BEEE-490D-BA7C-066C40B5E2B9}"); //msmpeg2adec.d11 
 
            Guid CLSID_MicrosoftDTVDVDVideoDecoder = new Guid("{212690FB-83E5-4526-8FD7-74478B7939CD}"); //msmpeg2vdec.d11 

            IBaseFilter pMicrosoftDTVDVDAudioDecoder2 = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MicrosoftDTVDVDAudioDecoder));
            hr = pGraph.AddFilter(pMicrosoftDTVDVDAudioDecoder2, "Microsoft DTV-DVD Audio Decoder"); checkHR(hr, "Can't add Microsoft DTV-DVD Audio Decoder to graph"); 

            //add Generic Network Provider 
            IBaseFilter pGenericNetworkProvider = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_GenericNetworkProvider)); 
            hr = pGraph.AddFilter(pGenericNetworkProvider, "Generic Network Provider");
            checkHR(hr, "Can't add Generic Network Provider to graph"); 
            //add AVerMedia BDA DVBS Tuner 
            IBaseFilter pAVerMediaBDADVBSTuner = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_AVerMediaBDADVBSTuner));
            hr = pGraph.AddFilter(pAVerMediaBDADVBSTuner, "AVerMedia BDA DVBS Tuner"); checkHR(hr, "Can't add AVerMedia BDA DVBS Tuner to graph"); 
            //connect Generic Network Provider and AVerMedia BDA DVBS Tuner 
            hr = pGraph.ConnectDirect(GetPin(pGenericNetworkProvider, "Antenna Out"), GetPin(pAVerMediaBDADVBSTuner, "Input0"), null);
            checkHR(hr, "Can't connect Generic Network Provider and AVerMedia BDA DVBS Tuner"); 
            //add AVerMedia BDA Digital Capture
            IBaseFilter pAVerMediaBDADigitalCapture = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_AVerMediaBDADigitalCapture));
            hr = pGraph.AddFilter(pAVerMediaBDADigitalCapture, "AVerMedia BDA Digital Capture"); checkHR(hr, "Can't add AVerMedia BDA Digital Capture to graph"); 
            //connect AVerMedia BDA DVBS Tuner and AVerMedia BDA Digital Capture 
            hr = pGraph.ConnectDirect(GetPin(pAVerMediaBDADVBSTuner, "MPEG2 Transport"), GetPin(pAVerMediaBDADigitalCapture, "MPEG2 Transport"), null); checkHR(hr, "Can't connect AVerMedia BDA DVBS Tuner and AVerMedia BDA Digital Capture"); 
            //add MPEG-2 Demultiplexer
            IBaseFilter pMPEG2Demultiplexer = (IBaseFilter) new MPEG2Demultiplexer();
            hr = pGraph.AddFilter(pMPEG2Demultiplexer, "MPEG-2 Demultiplexer");
            checkHR(hr, "Can't add MPEG-2 Demultiplexer to graph"); 
            //connect AVerMedia BDA Digital Capture and MPEG-2 Demultiplexer
            hr = pGraph.ConnectDirect(GetPin(pAVerMediaBDADigitalCapture, "MPEG2 Transport"), GetPin(pMPEG2Demultiplexer, "MPEG-2 Stream"), null);
            checkHR(hr, "Can't connect AVerMedia BDA Digital Capture and MPEG-2 Demultiplexer"); 
            //add MPEG-2 Sections and Tables 
            IBaseFilter pMPEG2SectionsandTables = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MPEG2SectionsandTables));
            hr = pGraph.AddFilter(pMPEG2SectionsandTables, "MPEG-2 Sections and Tables"); 
            checkHR(hr, "Can't add MPEG-2 Sections and Tables to graph"); 
            //add BDA MPEG2 Transport Information Filter 
            IBaseFilter pBDAMPEG2TransportInformationFilter = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_BDAMPEG2TransportInformationFilter));
            hr = pGraph.AddFilter(pBDAMPEG2TransportInformationFilter, "BDA MPEG2 Transport Information Filter"); checkHR(hr, "Can't add BDA MPEG2 Transport Information Filter to graph"); 
        //connect MPEG-2 Demultiplexer and BDA MPEG2 Transport Information Filter 
            hr = pGraph.ConnectDirect(GetPin(pMPEG2Demultiplexer, "001"), GetPin(pBDAMPEG2TransportInformationFilter, "IB Input"), null);
            checkHR(hr, "Can't connect MPEG-2 Demultiplexer and BDA MPEG2 Transport Information Filter"); 
            //connect MPEG-2 Demultiplexer and MPEG-2 Sections and Tables
            hr = pGraph.ConnectDirect(GetPin(pMPEG2Demultiplexer, "002"), GetPin(pMPEG2SectionsandTables, "In"), null);
            checkHR(hr, "Can't connect MPEG-2 Demultiplexer and MPEG-2 Sections and Tables");             
            


            //add Microsoft DTV-DVD Audio Decoder 
            IBaseFilter pMicrosoftDTVDVDAudioDecoder = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MicrosoftDTVDVDAudioDecoder));
            hr = pGraph.AddFilter(pMicrosoftDTVDVDAudioDecoder, "Microsoft DTV-DVD Audio Decoder"); checkHR(hr, "Can't add Microsoft DTV-DVD Audio Decoder to graph"); 
    
            //connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Audio Decoder
            hr = pGraph.ConnectDirect(GetPin(pMPEG2Demultiplexer, "007"), GetPin(pMicrosoftDTVDVDAudioDecoder, "XForm In"), null);
            checkHR(hr, "Can't connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Audio Decoder"); 
            
            //add WM ASF Writer
            IBaseFilter pWMASFWriter = (IBaseFilter) new WMAsfWriter();
            hr = pGraph.AddFilter(pWMASFWriter, "WM ASF Writer");
            checkHR(hr, "Can't add WM ASF Writer to graph");
            //set destination filename 
            IFileSinkFilter pWMASFWriter_sink = pWMASFWriter as IFileSinkFilter;
            if (pWMASFWriter_sink == null) 
            checkHR(unchecked((int)0x80004002), "Can't get IFileSinkFilter");
            hr = pWMASFWriter_sink.SetFileName(dstFile1, null); 
            checkHR(hr, "Can'i set filename"); 
            //connect Microsoft DTV-DVD Audio Decoder and WM ASF Writer
            hr = pGraph.ConnectDirect(GetPin(pMicrosoftDTVDVDAudioDecoder, "XFrom Out"), GetPin(pWMASFWriter, "Audio Input 01"), null);
            checkHR(hr, "Can't connect Microsoft DTV-DVD Audio Decoder and WM ASF Writer"); 
            //add Microsoft DTV-DVD Video Decoder 
            IBaseFilter pMicrosoftDTVDVDVideoDecoder = (IBaseFilter)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_MicrosoftDTVDVDVideoDecoder));
            hr = pGraph.AddFilter(pMicrosoftDTVDVDVideoDecoder, "Microsoft DTV-DVD Video Decoder"); checkHR(hr, "Can't add Microsoft DTV-DVD Video Decoder to graph"); 
            //connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Video Decoder 
            hr = pGraph.ConnectDirect(GetPin(pMPEG2Demultiplexer, "006"), GetPin(pMicrosoftDTVDVDVideoDecoder, "Video Input"), null);
            checkHR(hr, "Can't connect MPEG-2 Demultiplexer and Microsoft DTV-DVD Video Decoder"); 
            //connect Microsoft DTV-DVD Video Decoder and WM ASF Writer 
            hr = pGraph.ConnectDirect(GetPin(pMicrosoftDTVDVDVideoDecoder, "Video Output 1"), GetPin(pWMASFWriter, "Video Input 01"), null);
            checkHR(hr, "Can't connect Microsoft DTV-DVD Video Decoder and WM ASF Writer"); 
        }

    }

}

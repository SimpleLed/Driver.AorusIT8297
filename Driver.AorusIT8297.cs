using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using HidSharp;
using SimpleLed;
using DeviceTypes = SimpleLed.DeviceTypes;
using System.Windows.Controls;
using SimpleLedHelpers;
using Image = System.Drawing.Image;


namespace Driver.AorusIT8297
{
    public class IT8296Provider : ISimpleLed
    {

        public void SetDeviceOverride(ControlDevice controlDevice, CustomDeviceSpecification deviceSpec)
        {
            controlDevice.LEDs = new ControlDevice.LedUnit[deviceSpec.LedCount];

            for (int p = 0; p < deviceSpec.LedCount; p++)
            {
                controlDevice.LEDs[p] = new ControlDevice.LedUnit
                {
                    Data = new ControlDevice.LEDData
                    {
                        LEDNumber = p
                    },
                    LEDName = "LED " + (p + 1),
                    Color = new LEDColor(0, 0, 0)
                };
            }

            controlDevice.CustomDeviceSpecification = deviceSpec;
        }


        public List<CustomDeviceSpecification> GetCustomDeviceSpecifications()
        {
            return new List<CustomDeviceSpecification>();
        }

        const int HDR_BACK_IO = 0x20;
        const int HDR_CPU = 0x21;
        const int HDR_LED_2 = 0x22;
        const int HDR_PCIE = 0x23;
        const int HDR_LED_C1C2 = 0x24;
        const int HDR_D_LED1 = 0x25;
        const int HDR_D_LED2 = 0x26;
        const int HDR_LED_7 = 0x27;
        const int HDR_D_LED1_RGB = 0x58; // FIXME assuming that it is 0x58 for all boards
        const int HDR_D_LED2_RGB = 0x59;


        HidStream stream = null;

        private const int VENDOR_ID = 0x048D;
        private List<int> supportedIds = new List<int> { 0x8297 };
        public void Dispose()
        {

        }

        //private CustomConfig configXaml;

        public IT8296Provider()
        {
            
        }

        public void Configure(DriverDetails driverDetails)
        {
            
            //configXaml.SetLEDCounts = SetLedCounts;
            
            IT8297Config config = new IT8297Config();

            DeviceSetup();
        }

        private void DeviceSetup()
        {
            foreach (var dd in addedDevices)
            {
                DeviceRemoved?.Invoke(this, new Events.DeviceChangeEventArgs(dd));
            }

            var d = GetDevices();
            foreach (ControlDevice controlDevice in d)
            {
                addedDevices.Add(controlDevice);
                DeviceAdded.Invoke(this, new Events.DeviceChangeEventArgs(controlDevice));
            }
        }

        List<ControlDevice> addedDevices = new List<ControlDevice>();

        private void SetLedCounts(int arg1, int arg2)
        {
            ConfigData.ARGB1Leds = arg1;
            ConfigData.ARGB2Leds = arg2;
            isDirty = true;

        }

        public enum RGBSetter
        {
            Single,
            Strip,
            Composite
        }

        public class GigabyteRGBDevice
        {
            public RGBSetter Setter { get; set; }
            public int FirstAddress { get; set; }
            public int NumberOfLeds { get; set; }
            public RGBOrder RGBOrder { get; set; } = RGBOrder.RGB;
            public int[] CompositeOrder { get; set; }
        }

        public enum RGBOrder
        {
            RGB,
            RBG,
            BRG,
            BGR,
            GBR,
            GRB
        }

        public byte GetOrdered(LEDColor cl, RGBOrder order, int pos)
        {
            string r = order.ToString();
            string p = r.Substring(pos, 1);

            switch (p)
            {
                case "R": return (byte)cl.Red;
                case "G": return (byte)cl.Green;
                case "B": return (byte)cl.Blue;
            }

            return 0;
        }

        public LEDColor GetOrdered(LEDColor cl, RGBOrder order)
        {
            return new LEDColor(GetOrdered(cl, order, 0), GetOrdered(cl, order, 1), GetOrdered(cl, order, 2));
        }

        public Dictionary<string,string> GBMaps = new Dictionary<string, string>
        {
            {"B550 AORUS PRO", "STD_ATX"},
            {"B550 AORUS ELITE", "STD_ATX"},
            {"X570 AORUS ELITE", "STD_ATX"},
            {"X570 AORUS PRO WIFI", "STD_ATX"},
            {"X570 AORUS ULTRA", "STD_ATX"},
            {"B550I AORUS PRO AX", "ITX"},
            {"X570 I AORUS PRO WIFI", "ITX"},
            {"IT8297BX-GBX570", "FALLBACK"}
        };

        public Dictionary<string,string> GBoverrides = new Dictionary<string, string>
        {
            {"X570 I AORUS PRO WIFI", "MINI_ITX"},
            {"Z390 AORUS MASTER-CF","390"},
            {"Z390 AORUS ULTRA-CF","390"}
        };

        private string boardname = "Aorus";
        public List<IT8297ControlDevice> GetDevices()
        {
            var mbmanu = MotherboardInfo.Manufacturer;
            var mbmodel = MotherboardInfo.Model;
            var mbpn = MotherboardInfo.PartNumber;
            var mbproduct = MotherboardInfo.Product;
            var bm = MotherboardInfo.SystemName;

            var terp = new OpenConfiguration();
            terp.SetOption(OpenOption.Transient, true);

            var loader = new HidDeviceLoader();
            HidDevice device = null;
            HidSharp.HidDevice[] devices = null;
            foreach (var supportedId in supportedIds)
            {
                HidSharp.HidDevice[] tempdevices = loader.GetDevices(VENDOR_ID, supportedId).ToArray();
                if (tempdevices.Length > 0)
                {
                    devices = tempdevices;
                }
            }

            if (devices == null || devices.Length == 0)
            {
                return new List<IT8297ControlDevice>();
            }


            int attempts = 0;
            bool success = false;

            foreach (HidDevice ddevice in devices)
            {
                device = ddevice;
                try
                {
                    var ps = attempts % devices.Count();
                    Console.WriteLine("Trying connection "+ps);
                        //    device = devices[attempts % devices.Count()];
                    Console.WriteLine(device.DevicePath);
                    byte[] t = device.GetRawReportDescriptor();
                    Debug.WriteLine("got raw");
                    Console.WriteLine(device.GetFriendlyName());
                    Debug.WriteLine("got friendly name");
                    stream = device.Open(terp);
                    Debug.WriteLine("got strean");
                    stream.SetCalibration();
                    Debug.WriteLine("set callibration");
                    stream.SendPacket(0x60, 0);
                    success = true;
                }
                catch
                {
                    attempts++;
                    Thread.Sleep(100);
                }
            }

            if (!success)
            {
                return new List<IT8297ControlDevice>();
            }

            Bitmap pcieArea;
            Bitmap rgbPins;
            Bitmap vrm;

            Debug.WriteLine("Loading PCI Area png");
            
                pcieArea = (Bitmap)Image.FromStream(new MemoryStream(PCIArea_png.binary));
            

            Debug.WriteLine("Loading RGB Pins png");
            
                rgbPins = (Bitmap)Image.FromStream(new MemoryStream(rgbpins_png.binary));
            

            Debug.WriteLine("Loading VRM Area png");
           
                vrm = (Bitmap)Image.FromStream(new MemoryStream(VRM_png.binary));
           


            byte[] buffer = new byte[64];
            buffer[0] = 0xCC;
            stream.GetFeature(buffer);
            It8297ReportComplete report = GetReport(buffer);

            stream.SetLedCount();

            stream.Init();


            string name = report.ProductName;
            boardname = mbproduct;
            
            string layout = "";

            if (!GBMaps.ContainsKey(name))
            {
                name = "IT8297BX-GBX570";
            }

            layout = GBMaps[name];


            if (GBoverrides.ContainsKey(mbproduct))
            {
                layout = GBoverrides[mbproduct];
            }

            List<IT8297ControlDevice> result = new List<IT8297ControlDevice>();

            switch (layout)
            {
                case "FALLBACK":
                    {
                        result.Add(new IT8297ControlDevice
                        {
OverrideSupport = OverrideSupport.All,
                            
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "ARGB Header 1",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x58,
                                Setter = RGBSetter.Strip
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            OverrideSupport = OverrideSupport.All,
                            
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "ARGB Header 2",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x59,
                                Setter = RGBSetter.Strip
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "VRM Block",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 32,
                                Setter = RGBSetter.Single
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "PCI Area",
                            ProductImage = pcieArea,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 35,
                                Setter = RGBSetter.Single
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "C1C2 Header",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x24,
                                Setter = RGBSetter.Single
                            }
                        });



                        break;
                    }

                case "390":
                    {
                        result.Add(new IT8297ControlDevice
                        {
                            OverrideSupport = OverrideSupport.All,
                            
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "ARGB Header 1",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x58,
                                Setter = RGBSetter.Strip
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[21],
                            Driver = this,
                            DeviceType = DeviceTypes.MotherBoard,
                            Name = "VRM",
                            ProductImage = vrm,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x59,
                                Setter = RGBSetter.Strip,
                                RGBOrder = RGBOrder.GRB
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Thing 1",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x20,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Thing 2",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x21,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Thing 3",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x22,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Chipset",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x23,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Thing 4",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x24,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Thing 5",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x25,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Thing 6",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x26,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Thing 7",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x27,
                                Setter = RGBSetter.Single
                            }
                        });



                        break;
                    }

                case "MINI_ITX":
                    {
                        result.Add(new IT8297ControlDevice
                        {
                            OverrideSupport = OverrideSupport.All,
                            
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "ARGB Header 1",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x58,
                                Setter = RGBSetter.Strip
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Back I/O",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = HDR_BACK_IO,
                                Setter = RGBSetter.Single
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[4],
                            Driver = this,
                            Name = "MOBO Backlight",
                            ProductImage = pcieArea,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = HDR_CPU,
                                Setter = RGBSetter.Composite,
                                CompositeOrder = new int[]
                                {
                                    0x20,0x21,0x22,0x23
                                }
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "C1C2 Header",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x24,
                                Setter = RGBSetter.Single
                            }
                        });

                        //result.Add(new IT8297ControlDevice
                        //{
                        //    LEDs = new ControlDevice.LedUnit[1],
                        //    Driver = this,
                        //    Name = "PCIExpress",
                        //    ProductImage = pcieArea,
                        //    DeviceType = DeviceTypes.MotherBoard,
                        //    GigabyteRGBDevice = new GigabyteRGBDevice
                        //    {
                        //        FirstAddress = HDR_PCIE,
                        //        Setter = RGBSetter.Single
                        //    }
                        //});


                        //result.Add(new IT8297ControlDevice
                        //{
                        //    LEDs = new ControlDevice.LedUnit[1],
                        //    Driver = this,
                        //    DeviceType = DeviceTypes.Fan,
                        //    Name = "C1C2 Header",
                        //    ProductImage = rgbPins,
                        //    GigabyteRGBDevice = new GigabyteRGBDevice
                        //    {
                        //        FirstAddress = 0x24,
                        //        Setter = RGBSetter.Single
                        //    }
                        //});

                        break;
                    }

                case "ITX":
                    {
                        result.Add(new IT8297ControlDevice
                        {
                            OverrideSupport = OverrideSupport.All,
                           
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "ARGB Header 1",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x58,
                                Setter = RGBSetter.Strip
                            }
                        });
                        
                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Back I/O",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = HDR_BACK_IO,
                                Setter = RGBSetter.Single
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "CPU Header",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = HDR_CPU,
                                Setter = RGBSetter.Single
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "PCIExpress",
                            ProductImage = pcieArea,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = HDR_PCIE,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "C1C2 Header",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x24,
                                Setter = RGBSetter.Single
                            }
                        });

                        break;
                    }

                case "STD_ATX":
                    {
                        result.Add(new IT8297ControlDevice
                        {
                            OverrideSupport = OverrideSupport.All,
                            
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "ARGB Header 1",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x58,
                                Setter = RGBSetter.Strip
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            OverrideSupport = OverrideSupport.All,
                            
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "ARGB Header 2",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x59,
                                Setter = RGBSetter.Strip
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "Back I/O",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = HDR_BACK_IO,
                                Setter = RGBSetter.Single
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "CPU Header",
                            ProductImage = vrm,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = HDR_CPU,
                                Setter = RGBSetter.Single
                            }
                        });

                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            Name = "PCIExpress",
                            ProductImage = pcieArea,
                            DeviceType = DeviceTypes.MotherBoard,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = HDR_PCIE,
                                Setter = RGBSetter.Single
                            }
                        });


                        result.Add(new IT8297ControlDevice
                        {
                            LEDs = new ControlDevice.LedUnit[1],
                            Driver = this,
                            DeviceType = DeviceTypes.Fan,
                            Name = "C1C2 Header",
                            ProductImage = rgbPins,
                            GigabyteRGBDevice = new GigabyteRGBDevice
                            {
                                FirstAddress = 0x24,
                                Setter = RGBSetter.Single
                            }
                        });

                        break;
                    }
                    }


            foreach (IT8297ControlDevice it8297ControlDevice in result.Where(x=>x.LEDs == null || x.LEDs.Length<1))
            {
                SetDeviceOverride(it8297ControlDevice, new GenericFan());
            }


            foreach (IT8297ControlDevice it8297ControlDevice in result)
            {
                for (int i = 0; i < it8297ControlDevice.LEDs.Length; i++)
                {
                    it8297ControlDevice.LEDs[i] = new ControlDevice.LedUnit
                    {
                        Color = new LEDColor(0,0,0),
                        Data = new ControlDevice.LEDData
                        {
                            LEDNumber = i
                        },
                        LEDName = it8297ControlDevice.Name+" "+(i+1)
                    };
                }
            }



            return result;
        }

        public class IT8297ControlDevice : ControlDevice
        {
            public GigabyteRGBDevice GigabyteRGBDevice { get; set; }
        }

        public void Push(ControlDevice controlDevice)
        {
            IT8297ControlDevice cd = controlDevice as IT8297ControlDevice;

            switch (cd.GigabyteRGBDevice.Setter)
            {
                case RGBSetter.Strip:
                    stream.SendRGB(cd.LEDs.Select(x => GetOrdered(x.Color, cd.GigabyteRGBDevice.RGBOrder)).ToList(), (byte)cd.GigabyteRGBDevice.FirstAddress);
                    break;

                case RGBSetter.Single:
                    stream.SetLEDEffect((byte)cd.GigabyteRGBDevice.FirstAddress, 1, GetOrdered(cd.LEDs[0].Color, cd.GigabyteRGBDevice.RGBOrder, 0), GetOrdered(cd.LEDs[0].Color, cd.GigabyteRGBDevice.RGBOrder, 1), GetOrdered(cd.LEDs[0].Color, cd.GigabyteRGBDevice.RGBOrder, 2));
                    break;

                case RGBSetter.Composite:
                    for (int i = 0; i < cd.LEDs.Length; i++)
                    {
                        stream.SetLEDEffect((byte)cd.GigabyteRGBDevice.CompositeOrder[i], 1, GetOrdered(cd.LEDs[i].Color, cd.GigabyteRGBDevice.RGBOrder, 0), GetOrdered(cd.LEDs[i].Color, cd.GigabyteRGBDevice.RGBOrder, 1), GetOrdered(cd.LEDs[i].Color, cd.GigabyteRGBDevice.RGBOrder, 2));
                    }

                    break;
            }
        }


        public void Pull(ControlDevice controlDevice)
        {

        }

        public DriverProperties GetProperties()
        {
            return new DriverProperties
            {
                SupportsPush = true,
                IsSource = false,
                SupportsPull = false,
                SupportsCustomConfig = true,
                Id = Guid.Parse("49440d02-8ca3-4e35-a9a3-88b024cc0e2d"),
                Author = "mad ninja",
                CurrentVersion = new ReleaseNumber("1.0.0.20"),
                GitHubLink = "https://github.com/SimpleLed/Driver.AorusIT8297",
                Blurb = "Driver for Aorus motherboards featuring the IT8297 RGB controller.",
                IsPublicRelease = true,
                SetDeviceOverride = SetDeviceOverride
            };
        }


        GigabyteConfigModel ConfigData = new GigabyteConfigModel();
        public T GetConfig<T>() where T : SLSConfigData
        {
            
            SLSConfigData proxy = (SLSConfigData)ConfigData;
            return (T)proxy;
        }

        public void PutConfig<T>(T config) where T : SLSConfigData
        {
            
        }

        public UserControl GetCustomConfig(ControlDevice controlDevice)
        {
            return null;
        }

        private bool isDirty = false;
        public bool GetIsDirty()
        {
            return isDirty;
        }

        public void SetIsDirty(bool val)
        {

        }

        public string Name()
        {
            return boardname;
        }

        public void InterestedUSBChange(int VID, int PID, bool connected)
        {
        }

        public void SetColorProfile(ColorProfile value)
        {
            
        }

        public event Events.DeviceChangeEventHandler DeviceAdded;
        public event Events.DeviceChangeEventHandler DeviceRemoved;

        public event EventHandler DeviceRescanRequired;


        public static It8297ReportComplete GetReport(byte[] buffer)
        {
            IT8297_Report featureReport = buffer.ReadStruct<IT8297_Report>();
            byte[] str_product = new byte[32];
            Buffer.BlockCopy(buffer, 12, str_product, 0, 32);
            string ProductName = "";

            using (var ms = new MemoryStream(str_product))
            {
                using (var sr = new StreamReader(ms))
                {
                    ProductName = sr.ReadLine();
                }
            }

            for (int i = 0; i < ProductName.Length; i++)
            {
                if (ProductName.Substring(i, 1) == "\0")
                {
                    ProductName = ProductName.Substring(0, i);
                    break;
                }
            }

            return new It8297ReportComplete
            {
                ProductName = ProductName,
                report_id = featureReport.report_id,
                product = featureReport.product,
                device_num = featureReport.device_num,
                total_leds = featureReport.total_leds,
                fw_ver = featureReport.fw_ver,
                curr_led_count = featureReport.curr_led_count,
                reserved0 = featureReport.reserved0,
                byteorder0 = featureReport.byteorder0,
                byteorder1 = featureReport.byteorder1,
                byteorder2 = featureReport.byteorder2,
                chip_id = featureReport.chip_id,
                reserved1 = featureReport.reserved1

            };
        }

        [StructLayout(LayoutKind.Explicit, Size = 64, CharSet = CharSet.Ansi)]
        public class IT8297_Report
        {
            [FieldOffset(0)] public byte report_id;
            [FieldOffset(1)] public byte product;
            [FieldOffset(2)] public byte device_num;
            [FieldOffset(3)] public byte total_leds;
            [FieldOffset(4)] public UInt32 fw_ver;
            [FieldOffset(8)] public UInt16 curr_led_count;
            [FieldOffset(10)] public UInt16 reserved0;
            [FieldOffset(44)] public UInt32 byteorder0;
            [FieldOffset(48)] public UInt32 byteorder1;
            [FieldOffset(52)] public UInt32 byteorder2;
            [FieldOffset(56)] public UInt32 chip_id;
            [FieldOffset(60)] public UInt32 reserved1;
        };

        public class It8297ReportComplete : IT8297_Report
        {
            public string ProductName { get; set; }
            public Extensions.LEDCount LEDCount => (Extensions.LEDCount)total_leds;
        }


    }
}

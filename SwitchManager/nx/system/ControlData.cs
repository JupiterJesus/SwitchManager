using log4net;
using SwitchManager.util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SwitchManager.nx.system
{
    /// <summary>
    /// I don't know much about this. There is a file called control.nacp packed into the CONTROL NCA.
    /// It seems to contain metadata like language, version, title id, etc. It is alongside the game's icons,
    /// one for each supported language. The icon languages match the languages within the nacp file.
    /// I have seen some NSPs contain a file that ends in .nacp.xml. I have yet to read one of these files and compare
    /// it to the contents of the control.nacp file.
    /// 
    /// See http://switchbrew.org/index.php?title=Control.nacp.
    /// </summary>
    [XmlRoot("Application", Namespace = null, IsNullable = false)]
    public class ControlData
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ControlData));

        [XmlElement(ElementName = "Title")]
        public ControlTitle[] Titles { get; set; }

        [XmlElement(ElementName = "SupportedLanguage")]
        public string[] SupportedLanguages
        {
            get
            {
                List<string> r = new List<string>();
                for (uint i = 0, mask = 1; i < Titles.Length; i++, mask <<= 1)
                {
                    if ((SupportedLanguagesFlag & mask) != 0)
                        r.Add(((SwitchLanguage)i).ToString());
                }

                return r.ToArray();
            }
            set
            {
                int flag = 0;
                for (uint i = 0; i < value.Length; i++)
                {
                    string l = value[i];
                    if (!string.IsNullOrWhiteSpace(l))
                    {
                        if (Enum.TryParse(l, out SwitchLanguage lang))
                        {
                            flag |= (1 << (int)lang);
                        }
                    }
                }

                SupportedLanguagesFlag = flag;
            }
        }

        [XmlIgnore]
        public int SupportedLanguagesFlag { get; private set; }
        
        [XmlElement(ElementName = "ParentalControl")]
        public ParentalControlOption ParentalControl { get; set; }

        [XmlElement(ElementName = "Isbn")]
        public string Isbn { get; set; }

        [XmlElement(ElementName = "StartupUserAccount")]
        public StartupUserAccountOption StartupUserAccount { get; set; }

        [XmlElement(ElementName = "Screenshot")]
        public ScreenshotOption Screenshot { get; set; }

        [XmlElement(ElementName = "VideoCapture")]
        public VideoCaptureOption VideoCapture { get; set; }

        [XmlElement(ElementName = "TouchScreenUsagae")]
        public TouchScreenUsageOption TouchScreenUsage { get; set; }

        [XmlElement(ElementName = "PlayLogPolicy")]
        public PlayLogPolicyOption PlayLogPolicy { get; set; }

        [XmlElement(ElementName = "DataLossConfirmation")]
        public DataLossConfirmationOption DataLossConfirmation { get; set; }

        [XmlElement(ElementName = "Attribute")]
        public AttributeOption Attribute { get; set; }

        [XmlElement(ElementName = "PresenceGroupId")]
        public string PresenceGroupId { get; set; }

        [XmlElement(ElementName = "DisplayVersion")]
        public string DisplayVersion { get; set; }

        [XmlElement(ElementName = "AddOnContentBaseId")]
        public string AddOnContentBaseId { get; set; }

        [XmlElement(ElementName = "SaveDataOwnerId")]
        public string SaveDataOwnerId { get; set; }

        [XmlElement(ElementName = "UserAccountSaveDataSize")]
        public string UserAccountSaveDataSize { get; set; }

        [XmlElement(ElementName = "UserAccountSaveDataJournalSize")]
        public string UserAccountSaveDataJournalSize { get; set; }

        [XmlElement(ElementName = "DeviceSaveDataSize")]
        public string DeviceSaveDataSize { get; set; }

        [XmlElement(ElementName = "Rating")]
        public ControlRating[] Ratings { get; set; }

        [XmlElement(ElementName = "LocalCommunicationId")]
        public string[] LocalCommunicationIds { get; set; }

        [XmlElement(ElementName = "SeedForPseudoDeviceId")]
        public string SeedForPseudoDeviceId { get; set; }

        [XmlElement(ElementName = "BcatPassphrase")]
        public string BcatPassphrase { get; set; }

        [XmlElement(ElementName = "DeviceSaveDataJournalSize")]
        public string DeviceSaveDataJournalSize { get; set; }

        [XmlElement(ElementName = "BcatDeliveryCacheStorageSize")]
        public string BcatDeliveryCacheStorageSize { get; set; }

        [XmlElement(ElementName = "ApplicationErrorCodeCategory")]
        public string ApplicationErrorCodeCategory { get; set; }

        [XmlElement(ElementName = "LogoType")]
        public LogoTypeOption LogoType { get; set; }

        [XmlElement(ElementName = "LogoHandling")]
        public LogoHandlingOption LogoHandling { get; set; }

        [XmlElement(ElementName = "Icon")]
        public List<ControlIcon> Icons { get; set; }

        [XmlElement(ElementName = "CrashReport")]
        public CrashReportOption CrashReport { get; set; }

        [XmlElement(ElementName = "RuntimeAddOnContentInstall")]
        public RuntimeAddOnContentInstallOption RuntimeAddOnContentInstall { get; set; }

        [XmlElement(ElementName = "PlayLogQueryCapability")]
        public PlayLogQueryCapabilityOption PlayLogQueryCapability { get; set; }

        [XmlElement(ElementName = "ProgramIndex")]
        public uint ProgramIndex { get; set; }

        [XmlElement(ElementName = "AddOnContentRegistrationType")]
        public AddOnContentRegistrationTypeOption AddOnContentRegistrationType { get; set; }

        [XmlElement(ElementName = "UserAccountSaveDataSizeMax")]
        public string UserAccountSaveDataSizeMax { get; set; }

        [XmlElement(ElementName = "UserAccountSaveDataJournalSizeMax")]
        public string UserAccountSaveDataJournalSizeMax { get; set; }

        [XmlElement(ElementName = "DeviceSaveDataSizeMax")]
        public string DeviceSaveDataSizeMax { get; set; }

        [XmlElement(ElementName = "DeviceSaveDataJournalSizeMax")]
        public string DeviceSaveDataJournalSizeMax { get; set; }

        [XmlElement(ElementName = "TemporaryStorageSize")]
        public string TemporaryStorageSize { get; set; }

        [XmlElement(ElementName = "CacheStorageSize")]
        public string CacheStorageSize { get; set; }

        [XmlElement(ElementName = "CacheStorageJournalSize")]
        public string CacheStorageJournalSize { get; set; }

        [XmlElement(ElementName = "CacheStorageDataAndJournalSizeMax")]
        public string CacheStorageDataAndJournalSizeMax { get; set; }

        [XmlElement(ElementName = "CacheStorageIndexMax")]
        public string CacheStorageIndexMax { get; set; }

        [XmlElement(ElementName = "Hdcp")]
        public HdcpOption Hdcp { get; set; }

        [XmlIgnore]
        public byte[] Reserved { get; set; }

        public ControlData()
        {
        }

        public static ControlData Parse(string file)
        {
            using (var fs = File.OpenRead(file))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    ControlData data = new ControlData();

                    // Reading control.nacp
                    // See http://switchbrew.org/index.php?title=Control.nacp

                    string parent = Path.GetDirectoryName(file);
                    data.Titles = new ControlTitle[0x10];
                    data.Icons = new List<ControlIcon>();

                    // 0x0 + 0x3000 (0x10 entries of 0x300 each) - language entries for developer name and title name
                    for (int i = 0; i < data.Titles.Length; i++)
                    {
                        ControlTitle title = new ControlTitle();
                        title.Name = br.ReadUTF8NullTerminated(0x200);
                        title.Publisher = br.ReadUTF8NullTerminated(0x100);
                        title.Language = (SwitchLanguage)i;

                        if (!string.IsNullOrWhiteSpace(title.Name))
                            data.Titles[i] = title;

                        string icon = parent + Path.DirectorySeparatorChar + "icon_" + title.Language + ".bin";
                        if (File.Exists(icon))
                        {
                            string hash = Crypto.ComputeHash(File.OpenRead(icon)).ToHex();
                            data.Icons.Add(new ControlIcon { Language = title.Language, NxIconHash = hash });
                        }
                    }

                    // 0x3000 + 0x25 ISBN
                    data.Isbn = br.ReadUTF8NullTerminated(0x25);
                    if (string.IsNullOrWhiteSpace(data.Isbn)) data.Isbn = null;

                    // 0x3025 + 0x3 TouchScreenUsage, StartupUserAccount, ???
                    byte sa = br.ReadByte(); data.StartupUserAccount = (StartupUserAccountOption)sa;
                    byte t = br.ReadByte(); data.TouchScreenUsage = (TouchScreenUsageOption)t;
                    br.ReadByte();

                    // 0x3028 + 0x4 Attribute? None, Demo
                    uint a = br.ReadUInt32(); data.Attribute = (AttributeOption)a;

                    // 0x302C + 0x4 Supported languages, one bit for each supported language
                    // AmericanEnglish is the LSB, value&1. There are only 15 languages, so the 17 MS bits are 0
                    // If all languages are supported, the value if 00 00 7F FF (FF 7F 00 00 when written to disk)
                    data.SupportedLanguagesFlag = br.ReadInt32();

                    // 0x3030 + 0x4 Parental Control options flag
                    data.ParentalControl = (ParentalControlOption)br.ReadInt32();

                    // 0x3034 + 0x4 VideoCapture, Screenshot, DataLossConfirmation, PlayLogPolicy
                    byte v = br.ReadByte(); data.VideoCapture = (VideoCaptureOption)v;
                    byte s = br.ReadByte(); data.Screenshot = (ScreenshotOption)s;
                    byte d = br.ReadByte(); data.DataLossConfirmation = (DataLossConfirmationOption)d;
                    byte p = br.ReadByte(); data.PlayLogPolicy = (PlayLogPolicyOption)p;

                    // 0x3038 + 0x8 presence group ID or savedataownerid
                    data.PresenceGroupId = "0x" + br.ReadHex64();

                    // 0x3040 + 0x20 RatingAge
                    byte[] rating = br.ReadBytes(0x20);
                    data.Ratings = new ControlRating[rating.Length];
                    for (int i = 0; i < rating.Length; i++)
                    {
                        byte r = rating[i];
                        if (r == 0xFF) // not a valid age thus not supported
                            data.Ratings[i] = null;
                        else
                            data.Ratings[i] = new ControlRating { Organisation = (RatingOrganisation)i, Age = r, };
                    }

                    // 0x3060 + 0x10 Application version string
                    data.DisplayVersion = br.ReadUTF8NullTerminated(0x10);

                    // 0x3070 + 0x8 Base titleID for DLC, set even when DLC is not used. Usually app_titleID+0x1000
                    data.AddOnContentBaseId = "0x" + br.ReadHex64();

                    // 0x3078 + 0x8 presence group ID or savedataownerid
                    data.SaveDataOwnerId = "0x" + br.ReadHex64();

                    // 0x3080 + 0x8 UserAccountSaveDataSize
                    data.UserAccountSaveDataSize = "0x" + br.ReadHex64();

                    // 0x3088 + 0x8 UserAccountSaveDataSize
                    data.UserAccountSaveDataJournalSize = "0x" + br.ReadHex64();

                    // 0x3090 + 0x8 DeviceSaveDataSize
                    data.DeviceSaveDataSize = "0x" + br.ReadHex64();

                    // 0x3098 + 0x8 DeviceSaveDataJournalSize
                    data.DeviceSaveDataJournalSize = "0x" + br.ReadHex64();

                    // 0x30A0 + 0x8 BcatDeliveryCacheStorageSize
                    data.BcatDeliveryCacheStorageSize = "0x" + br.ReadHex64();

                    // 0x30A8 + 0x8 ApplicationErrorCodeCategory
                    data.ApplicationErrorCodeCategory = br.ReadUTF8NullTerminated(0x8);
                    if (string.IsNullOrWhiteSpace(data.ApplicationErrorCodeCategory)) data.ApplicationErrorCodeCategory = null;

                    // 0x30b0 + (0x8) * 0x8 LocalCommunicationId (array of 8) - just the title ID 8 times?
                     data.LocalCommunicationIds = new string[0x8];
                    for (int i = 0; i < 0x8; i++)
                        data.LocalCommunicationIds[i] = "0x" + br.ReadHex64();

                    // 0x30F0 + 0x4 LogoType ??
                    data.LogoType = (LogoTypeOption)br.ReadInt32();

                    // 0x30F4 + 0x4 LogoHandling ??
                    data.LogoHandling = (LogoHandlingOption)br.ReadInt32();

                    // 0x30F8 + 0x8 SeedForPseudoDeviceId
                    data.SeedForPseudoDeviceId = "0x" + br.ReadHex64();

                    // 0x3100 + 0x40 BcatPassphrase (0 when unused)
                    data.BcatPassphrase = br.ReadUTF8NullTerminated(0x40);
                    if (string.IsNullOrWhiteSpace(data.BcatPassphrase)) data.BcatPassphrase = null;

                    // The following fields are not for sure. I just guessed at their sizes. The fact that
                    // they all added up to exactly 0x60 bytes made me feel confident about it

                    data.AddOnContentRegistrationType = (AddOnContentRegistrationTypeOption)br.ReadUInt32();

                    data.UserAccountSaveDataSizeMax = "0x" + br.ReadHex64();

                    data.UserAccountSaveDataJournalSizeMax = "0x" + br.ReadHex64();

                    data.DeviceSaveDataSizeMax = "0x" + br.ReadHex64();

                    data.DeviceSaveDataJournalSizeMax = "0x" + br.ReadHex64();

                    data.TemporaryStorageSize = "0x" + br.ReadHex64();

                    data.CacheStorageSize = "0x" + br.ReadHex64();

                    data.CacheStorageJournalSize = "0x" + br.ReadHex64();

                    data.CacheStorageDataAndJournalSizeMax = "0x" + br.ReadHex64();

                    data.CacheStorageIndexMax = "0x" + br.ReadHex64();

                    data.Hdcp = (HdcpOption)br.ReadUInt32();

                    data.CrashReport = (CrashReportOption)br.ReadUInt32();

                    data.RuntimeAddOnContentInstall = (RuntimeAddOnContentInstallOption)br.ReadUInt32();

                    data.PlayLogQueryCapability = (PlayLogQueryCapabilityOption)br.ReadUInt32();

                    data.ProgramIndex = br.ReadUInt32();
                    
                    // 0x31A0 + 0xE60 Normally all-zero?
                    data.Reserved = br.ReadBytes(0xE60);

                    return data;
                }
            }
        }

        internal void GenerateXml(string controlXmlFile)
        {
            // Create a new file stream to write the serialized object to a file
            using (TextWriter writer = new StreamWriter(controlXmlFile))
            {
                XmlSerializer xmls = new XmlSerializer(typeof(ControlData));
                XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                xmls.Serialize(writer, this, ns);
            }

            logger.Info($"Generated XML file {Path.GetFileName(controlXmlFile)}!");
        }
    }

    [XmlRoot(ElementName = "Title")]
    public class ControlTitle
    {
        [XmlElement(ElementName = "Language")]
        public SwitchLanguage Language { get; set; }

        [XmlElement(ElementName = "Name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "Publisher")]
        public string Publisher { get; set; }
    }

    [XmlRoot(ElementName = "Rating")]
    public class ControlRating
    {
        [XmlElement(ElementName = "Organisation")]
        public RatingOrganisation Organisation { get; set; }

        [XmlElement(ElementName = "Age")]
        public byte Age { get; set; }
    }

    [XmlRoot(ElementName = "Icon")]
    public class ControlIcon
    {
        [XmlElement(ElementName = "Language")]
        public SwitchLanguage Language { get; set; }

        [XmlElement(ElementName = "NxIconHash")]
        public string NxIconHash { get; set; }
    }

    public enum VideoCaptureOption { Deny, Allow }
    public enum ScreenshotOption { Allow, Deny }
    public enum StartupUserAccountOption { None, Required, RequiredWithNetworkServiceAccountAvailable }
    public enum TouchScreenUsageOption { None, Supported, Required }
    public enum AttributeOption { None, Demo }
    public enum PlayLogPolicyOption { All, LogOnly, PL_None }
    public enum DataLossConfirmationOption { None, Required }
    public enum ParentalControlOption { None, FreeCommunication }
    public enum LogoTypeOption { LicensedByNintendo, DistributedByNintendo, Nintendo }
    public enum LogoHandlingOption { Auto, Unknown_1, Unknown_2, Unknown_3, Unknown_4 } // I have no source for what this should be called
    public enum AddOnContentRegistrationTypeOption { OnDemand, Unknown_1, Unknown_2, Unknown_3, Unknown_4 }
    public enum HdcpOption { None, Unknown_1, Unknown_2, Unknown_3, Unknown_4 }
    public enum CrashReportOption { Deny, Allow, Unknown_2, Unknown_3, Unknown_4 }
    public enum RuntimeAddOnContentInstallOption { Deny, Allow, Unknown_2, Unknown_3, Unknown_4 }
    public enum PlayLogQueryCapabilityOption { None, Unknown_1, Unknown_2, Unknown_3, Unknown_4 }
}

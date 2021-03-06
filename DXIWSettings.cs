﻿using System;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.DXIW
{
    public partial class DXIWSettings : UserControl
    {
        public bool AutoReset { get; set; }
        public bool AutoStart { get; set; }

        public bool UseNonSafeMemoryReading { get; set; }

        private const bool DEFAULT_AUTORESET = false;
        private const bool DEFAULT_AUTOSTART = true;
        private const bool DEFAULT_UNSAFEREADER = true;

        public DXIWSettings()
        {
            InitializeComponent();

            this.chkUnsafeReading.DataBindings.Add("Checked", this, "UseNonSafeMemoryReading", false, DataSourceUpdateMode.OnPropertyChanged);

            this.chkAutoReset.DataBindings.Add("Checked", this, "AutoReset", false, DataSourceUpdateMode.OnPropertyChanged);
            this.chkAutoStart.DataBindings.Add("Checked", this, "AutoStart", false, DataSourceUpdateMode.OnPropertyChanged);


            // defaults
            this.UseNonSafeMemoryReading = DEFAULT_UNSAFEREADER;
            this.AutoReset = DEFAULT_AUTORESET;
            this.AutoStart = DEFAULT_AUTOSTART;
        }

        public XmlNode GetSettings(XmlDocument doc)
        {
            XmlElement settingsNode = doc.CreateElement("Settings");

            settingsNode.AppendChild(ToElement(doc, "Version", Assembly.GetExecutingAssembly().GetName().Version.ToString(3)));

            settingsNode.AppendChild(ToElement(doc, "NonSafeMemoryReader", this.UseNonSafeMemoryReading));
            settingsNode.AppendChild(ToElement(doc, "AutoReset", this.AutoReset));
            settingsNode.AppendChild(ToElement(doc, "AutoStart", this.AutoStart));

            return settingsNode;
        }

        public void SetSettings(XmlNode settings)
        {
            this.UseNonSafeMemoryReading = ParseBool(settings, "NonSafeMemoryReader", DEFAULT_UNSAFEREADER);
            this.AutoReset = ParseBool(settings, "AutoReset", DEFAULT_AUTORESET);
            this.AutoStart = ParseBool(settings, "AutoStart", DEFAULT_AUTOSTART);
        }

        static bool ParseBool(XmlNode settings, string setting, bool default_ = false)
        {
            bool val;
            return settings[setting] != null ?
                (Boolean.TryParse(settings[setting].InnerText, out val) ? val : default_)
                : default_;
        }

        static XmlElement ToElement<T>(XmlDocument document, string name, T value)
        {
            XmlElement str = document.CreateElement(name);
            str.InnerText = value.ToString();
            return str;
        }
    }
}

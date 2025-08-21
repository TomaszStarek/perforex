using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace Wiring
{
    public class Wire
    {
        public string NameOfCabinet { get; set; } = "";
        public string Number { get; set; } = "";
        public string Nc { get; set; } = "";
        public string Torque { get; set; } = "";
        public string Descriptions { get; set; } = "";

        public string Bus { get; set; } = "";
        public string Box { get; set; } = "";


        public double CrossSection { get; set; } = 0.0;
        public string Type { get; set; } = "";
        public double Lenght { get; set; } = 0.0;

        public bool IsCoordinatesRequired { get; set; } = false;
        public string X { get; set; } = "";
        public string Y { get; set; } = "";

        public string hostname { get; set; } = "";
        public string program { get; set; } = "";
        public string job { get; set; } = "";
        public bool IsCameraNeeded { get; set; } = false;
        public bool CameraResult { get; set; } = false;

        public string DtSource { get; set; } = "";
        public string WireEndTerminationSource { get; set; } = "";
        public string DtTarget { get; set; } = "";
        public string WireEndDimensionSource { get; set; } = "";
        public string WireEndDimensionTarget { get; set; } = "";
        public string WireEndTerminationTarget { get; set; } = "";
        public string Colour { get; set; } = "";
        public double? Progress { get; set; } = 0;
        public DateTime Start { get; set; } = DateTime.Now;
        public DateTime DateOfFinish { get; set; } = DateTime.Now;
        public string? MadeBy { get; set; } = "";

        public bool IsConfirmed { get; set; } = false;
        public int? WireStatus { get; set; } = 0;
        public double Seconds { get; set; } = 0;
        public string? Addnotations { get; set; }

        public double TimeForExecuting { get; set; }
        public bool Overtime { get; set; }
        public double SecondsSource { get; set; }
        public double SecondsDestination { get; set; }
        public string? ReasonDT { get; set; }
        public double HandlingTime { get; set; }

        public override string ToString()
        {
            return this.Number + ", " + this.DtSource + "";
        }


    }
}

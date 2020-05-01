using Grasshopper.Kernel;
using Rhino;
using Rhino.DocObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Heron
{
    public abstract class HeronComponent : GH_Component
    {
        public HeronComponent(string name, string nickName, string description, string subCategory) : base(name, nickName, description, "Heron", subCategory)
        {
            if (DependsOnEarthAnchorPoint)
            {
                EarthAnchorPointChanged += HandleEarthAnchorPointChange;
            }
            else
            {
                //this bit is probably unnecessary
                EarthAnchorPointChanged -= HandleEarthAnchorPointChange;
            }
        }

        private void HandleEarthAnchorPointChange(object sender, EventArgs e)
        {
            OnPingDocument().ScheduleSolution(5, (doc) =>
            {
                ExpireSolution(false);
            });
        }

        internal virtual bool DependsOnEarthAnchorPoint { get; set; } = false;

        public static EarthAnchorPoint EarthAnchorPoint
        {
            get
            {
                return RhinoDoc.ActiveDoc.EarthAnchorPoint;
            }
            set
            {
                RhinoDoc.ActiveDoc.EarthAnchorPoint = value;
                OnEarthAnchorPointChanged(new EventArgs());
            }
        }

        public static event EventHandler EarthAnchorPointChanged;

        protected static void OnEarthAnchorPointChanged(EventArgs e)
        {
            var handler = EarthAnchorPointChanged;
            handler?.Invoke(null, e);
        }

        public static void SetEarthAnchorPoint(double latitude, double longitude)
        {
            EarthAnchorPoint = new EarthAnchorPoint()
            {
                EarthBasepointLatitude = latitude,
                EarthBasepointLongitude = longitude
            };
        }
    }
}

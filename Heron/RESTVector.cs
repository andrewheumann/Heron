﻿using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;
using GH_IO;
using GH_IO.Serialization;

using Newtonsoft.Json.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Serialization;

namespace Heron
{
    public class RESTVector : HeronComponent
    {
        //Class Constructor
        public RESTVector() : base("Get REST Vector", "RESTVector", "Get vector data from ArcGIS REST Services", "GIS REST")
        {

        }


        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Boundary", "boundary", "Boundary curve(s) for vector data", GH_ParamAccess.list);
            pManager.AddTextParameter("REST URL", "URL", "ArcGIS REST Service website to query", GH_ParamAccess.item);
            pManager.AddBooleanParameter("run", "get", "Go ahead to download vector data from the Service", GH_ParamAccess.item, false);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("fieldNames", "fieldNames", "List of data fields associated with vectors", GH_ParamAccess.list);
            pManager.AddTextParameter("fieldValues", "fieldValues", "Data values associated with vectors", GH_ParamAccess.tree);
            pManager.AddPointParameter("featurePoints", "featurePoints", "Points of vector data", GH_ParamAccess.tree);
            pManager.AddTextParameter("RESTQuery", "RESTQuery", "Full text of REST query", GH_ParamAccess.tree);

        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> boundary = new List<Curve>();
            DA.GetDataList<Curve>(0, boundary);

            string URL = "";
            DA.GetData<string>("REST URL", ref URL);

            bool run = false;
            DA.GetData<bool>("run", ref run);

            ///TODO: implement SetCRS here.
            ///Option to set CRS here to user-defined.  Needs a SetCRS global variable.
            int SRef = 3857;

            GH_Structure<GH_String> mapquery = new GH_Structure<GH_String>();
            GH_Structure<GH_ObjectWrapper> jT = new GH_Structure<GH_ObjectWrapper>();
            List<JObject> j = new List<JObject>();

            GH_Structure<GH_Point> restpoints = new GH_Structure<GH_Point>();
            GH_Structure<GH_String> attpoints = new GH_Structure<GH_String>();
            GH_Structure<GH_String> fieldnames = new GH_Structure<GH_String>();


            for (int i = 0; i < boundary.Count; i++)
            {

                GH_Path cpath = new GH_Path(i);
                Point3d min = Heron.Convert.ToWGS(boundary[i].GetBoundingBox(true).Min);
                Point3d max = Heron.Convert.ToWGS(boundary[i].GetBoundingBox(true).Max);

                string restquery = URL +
                  "query?where=&text=&objectIds=&time=&geometry=" + Heron.Convert.ConvertLat(min.X, SRef) + "%2C" + Heron.Convert.ConvertLon(min.Y, SRef) + "%2C" + Heron.Convert.ConvertLat(max.X, SRef) + "%2C" + Heron.Convert.ConvertLon(max.Y, SRef) +
                  "&geometryType=esriGeometryEnvelope&inSR=" + SRef +
                  "&spatialRel=esriSpatialRelIntersects&relationParam=&outFields=*&returnGeometry=true&maxAllowableOffset=&geometryPrecision=" +
                  "&outSR=" + SRef +
                  "&returnIdsOnly=false&returnCountOnly=false&orderByFields=&groupByFieldsForStatistics=&outStatistics=&returnZ=false&returnM=false&gdbVersion=&returnDistinctValues=false&f=json";

                mapquery.Append(new GH_String(restquery), cpath);

                string result = GetData(restquery);


                jT.Append(new GH_ObjectWrapper(JsonConvert.DeserializeObject<JObject>(result)), cpath);
                j.Add(JsonConvert.DeserializeObject<JObject>(result));

                JArray e = (JArray)j[i]["features"];

                for (int m = 0; m < e.Count; m++)
                {
                    JObject aa = (JObject)j[i]["features"][m]["attributes"];
                    GH_Path path = new GH_Path(i, m);

                    //need to be able to escape this if no "geometry" property
                    if (j[i].Property("features.[" + m + "].geometry") != null)
                    {
                        //choose type of geometry to read
                        JsonReader jreader = j[i]["features"][m]["geometry"].CreateReader();
                        int jrc = 0;
                        string gt = null;
                        while ((jreader.Read()) && (jrc < 1))
                        {
                            if (jreader.Value != null)
                            {
                                //gtype.Add(jreader.Value, path);
                                gt = jreader.Value.ToString();
                                jrc++;
                            }
                        }

                        JArray c = (JArray)j[i]["features"][m]["geometry"][gt][0];
                        for (int k = 0; k < c.Count; k++)
                        {
                            double xx = (double)j[i]["features"][m]["geometry"][gt][0][k][0];
                            double yy = (double)j[i]["features"][m]["geometry"][gt][0][k][1];
                            restpoints.Append(new GH_Point(Heron.Convert.ConvertXY(xx, yy, SRef)), path);
                        }
                    }


                    foreach (JProperty attribute in j[i]["features"][m]["attributes"])
                    {
                        attpoints.Append(new GH_String(attribute.Value.ToString()), path);
                    }
                }

                //Get the field names
                foreach (JObject fn in j[i]["fields"])
                {
                    fieldnames.Append(new GH_String(fn["alias"].Value<string>()), cpath);
                }
            }

            DA.SetDataList(0, fieldnames.get_Branch(0));
            DA.SetDataTree(1, attpoints);
            DA.SetDataTree(2, restpoints);
            DA.SetDataTree(3, mapquery);

        }

        //Return JSON from webquery
        public static string GetData(string qst)
        {
            System.Net.HttpWebRequest req = System.Net.WebRequest.Create(qst) as System.Net.HttpWebRequest;
            string result = null;
            try
            {
                using (System.Net.HttpWebResponse resp = req.GetResponse() as System.Net.HttpWebResponse)
                {
                    System.IO.StreamReader reader = new System.IO.StreamReader(resp.GetResponseStream());
                    result = reader.ReadToEnd();
                    reader.Close();
                }
            }
            catch
            {
                return "Something went wrong getting data from the Service";
            }
            return result;
        }



        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return Properties.Resources.vector;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("{3E93C79E-954C-4074-8637-E1B9BDC8B367}"); }
        }
    }
}

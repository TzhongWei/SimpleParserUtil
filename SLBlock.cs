using Rhino.Geometry;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;

namespace SimpleParserUtil
{
    public class SLBlock
    {
        private double Size { get; }
        private Color[] Colours { get; } = new Color[2];
        private string Name { get; }
        private int BlockID { get; }
        private static Brep[] SLBlockBrep(double Size)
        {
            var Blocks = new Brep[2];
            var OriginPoint = new Point3d(-Size * 2, 0, -Size * 2);
            List<Point3d> Contour_1 = new List<Point3d>(){OriginPoint,
            OriginPoint + new Vector3d(Size * 3, 0, 0),
            OriginPoint + new Vector3d(Size * 3, 0, Size),
            OriginPoint + new Vector3d(Size, 0, Size),
            OriginPoint + new Vector3d(Size, 0, Size * 2),
            OriginPoint + new Vector3d(0, 0, Size * 2),
            OriginPoint
            };
            PolylineCurve Curve_1 = new PolylineCurve(Contour_1);
            List<Point3d> Contour_2 = new List<Point3d>(){
            OriginPoint + new Vector3d(Size * 2, 0, 0),
            OriginPoint + new Vector3d(Size * 3, 0, 0),
            OriginPoint + new Vector3d(Size * 3, 0, Size),
            OriginPoint + new Vector3d(Size * 4, 0, Size),
            OriginPoint + new Vector3d(Size * 4, 0, Size * 3),
            OriginPoint + new Vector3d(Size * 3, 0, Size * 3),
            OriginPoint + new Vector3d(Size * 3, 0, Size * 2),
            OriginPoint + new Vector3d(Size * 2, 0, Size * 2),
            OriginPoint + new Vector3d(Size * 2, 0, 0)
            };
            PolylineCurve Curve_2 = new PolylineCurve(Contour_2);
            var Extrude_1 = Extrusion.Create(Curve_1, Size, true).ToBrep();
            var Extrude_2 = Extrusion.Create(Curve_2, -Size, true).ToBrep();
            Brep SL_Block = Brep.CreateBooleanUnion(new List<Brep>() { Extrude_1, Extrude_2 }, 0.1)[0];
            SL_Block.MergeCoplanarFaces(0.1, 0.1);


            if (Size > 1)
            {

                List<int> edgeInt = new List<int>();
                List<double> Radius = new List<double>();
                for (int i = 0; i < SL_Block.Edges.Count; i++)
                {
                    edgeInt.Add(i);
                    Radius.Add(Size * 0.1);
                }
                SL_Block = Brep.CreateFilletEdges(SL_Block, edgeInt, Radius, Radius, BlendType.Chamfer, RailType.DistanceFromEdge, 0.1)[0];
                Curve[] OuterCurve = SL_Block.DuplicateNakedEdgeCurves(true, false);
                List<Brep> JoinBreps = new List<Brep>();
                foreach (Curve cur in Curve.JoinCurves(SL_Block.DuplicateNakedEdgeCurves(true, false), 0.1))
                {
                    var Segs = cur.DuplicateSegments();
                    var Cap_Srf = Brep.CreateEdgeSurface(Segs);
                    JoinBreps.Add(Cap_Srf);
                }

                JoinBreps.Add(SL_Block);
                SL_Block = Brep.JoinBreps(JoinBreps, 0.1)[0];
            }
            SL_Block.Rotate(Math.PI, Vector3d.ZAxis, Point3d.Origin);
            Blocks[0] = SL_Block.DuplicateBrep();
            SL_Block.Rotate(Math.PI, Vector3d.YAxis, Point3d.Origin);
            Blocks[1] = SL_Block.DuplicateBrep();
            return Blocks;
        }

        private static Dictionary<string, Transform> SLBlockTS(double Size = 5)
        {
            var Voca = new Dictionary<string, Transform>();
            string[] Labels = new string[] { "H", "A", "D", "S", "T", "Y" };
            double[] TranslationX = new double[] {
                Size * 2,
                Size,
                Size * 2,
                Size,
                Size,
                Size
            };
            double[] TranslationY = new double[] {
                0,
                -Size,
                0,
                Size,
                -Size,
                Size
            };
            double[] TranslationZ = new double[] {
                0,
                0,
                -Size,
                -Size,
                Size,
                -Size * 2
            };
            double[] RotateX = new double[]
            {
                0, -90, 0, 90, -90, 90
            };
            double[] RotateZ = new double[]
            {
                180, 0, 0, 180, 180, 0
            };
            for (int i = 0; i < 6; i++)
            {
                var MxTranslation = Transform.Translation(new Vector3d(TranslationX[i], TranslationY[i], TranslationZ[i]));
                var MxRotateX = Transform.Rotation(RotateX[i] * Math.PI / 180, Point3d.Origin);
                var MxRotateZ = Transform.Rotation(-RotateZ[i] * Math.PI / 180, Vector3d.XAxis, Point3d.Origin);
                Voca.Add(Labels[i], MxTranslation * MxRotateX * MxRotateZ);
            }
            return Voca;
        }

        public SLBlock(double Size, string Name) : this(Size, Name, new List<Color> { Color.Red, Color.Red })
        {

        }
        public SLBlock(double Size, string Name, List<Color> Cols)
        {
            this.Size = Size;
            this.Name = Name;
            if (Cols.Count > 1)
            {
                this.Colours[0] = Cols[0];
                this.Colours[1] = Cols[1];
            }
            else
            {
                this.Colours[0] = Cols[0];
                this.Colours[1] = Cols[0];
            }
        }
        public static int FindSLBlock(string Name)
        {
            var ID = -1;
            var Doc = RhinoDoc.ActiveDoc;
            try { ID = Doc.InstanceDefinitions.Find(Name).Index; }
            catch { ID = -1; }
            return ID;
        }
        private static int TryFindLayer()
        {
            var ID = -1;
            var Doc = RhinoDoc.ActiveDoc;
            try { ID = Doc.Layers.FindName("SLBlockLayer").Index; }
            catch { ID = -1; }
            return ID;
        }
        public int SetBlock()
        {
            var Doc = RhinoDoc.ActiveDoc;
            int LayerIndex = -1;
            if ((LayerIndex = TryFindLayer()) == -1)
            {
                var NewLayer = new Rhino.DocObjects.Layer
                {
                    Name = "SLBlockLayer",
                    Color = Color.Black
                };
                LayerIndex = Doc.Layers.Add(NewLayer);
            }
            var Att1 = new Rhino.DocObjects.ObjectAttributes
            {
                LayerIndex = LayerIndex,
                ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                ObjectColor = Colours[0]
            };
            var Att2 = new Rhino.DocObjects.ObjectAttributes
            {
                LayerIndex = LayerIndex,
                ColorSource = Rhino.DocObjects.ObjectColorSource.ColorFromObject,
                ObjectColor = Colours[0]
            };
            var ID = Doc.InstanceDefinitions.Add(
                this.Name, "SLBlock_" + this.Name,
                Point3d.Origin,
                SLBlockBrep(this.Size),
                new Rhino.DocObjects.ObjectAttributes[] { Att1, Att2 }
            );

            var Voca = SLBlockTS(this.Size);
            var Instance = Doc.InstanceDefinitions.Find(this.Name);
            Instance.SetUserString("Name", this.Name);
            Instance.SetUserString("ID", "UNSET");
            Instance.SetUserString("TransformToken", "H,A,D,S,T,Y");
            foreach (var Kvp in Voca)
            {
                Instance.SetUserString(Kvp.Key, TransformToJson(Kvp.Value));
            }
            return ID;
        }
        public static string TransformToJson(Transform TS)
        => JsonSerializer.Serialize<Transform>(TS);

        public static Transform TransformFromJson(string Str)
        => JsonSerializer.Deserialize<Transform>(Str);
    }
}

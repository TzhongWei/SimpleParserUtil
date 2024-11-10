using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SimpleParserUtil
{
    public static class VoxelPath
    {
        public static void GetPathSentence(
            Curve StPath,
            Vector3d Sth1,
            Vector3d Stv2,
            double CellDim,
            ref string Sentence,
            ref Transform TS
            )
        {
            var PL = new Plane(StPath.PointAtStart, Stv2, Sth1);
            var Cell = ReorientedRec(StPath.PointAtStart, RectangleGet(CellDim, Stv2, Sth1));
            var AdjustPt = StPath.PointAtStart - Sth1 * CellDim / 4;
            TS = Transform.PlaneToPlane(Plane.WorldXY, new Plane(AdjustPt, Stv2, -Sth1));
            var StartSentence = "#S";
            var ts = new List<double>();
            double t = Intersection.CurveCurve(StPath, Cell, 0.2, 0.1).ToList()[0].ParameterA;
            int CC = 0;
            while (true)
            {
                if (ts.Contains(Math.Round(t, 4))) break;
                else
                {
                    var Tangent = StPath.TangentAt(t);
                    ts.Add(Math.Round(t, 4));
                    Tangent *= CellDim;
                    Cell.Translate(Tangent);
                    var tsTemp = Intersection.CurveCurve(StPath, Cell, 0.1, 0.1).Select(x => x.ParameterA).ToList();
                    t = tsTemp.Count == 1 ? tsTemp[0] : tsTemp.Last();
                    if (tsTemp.Count > 1)
                    {
                        var TestVector = StPath.TangentAt(t);
                        Tangent.Unitize();
                        var angle = Vector3d.VectorAngle(Tangent, TestVector, PL);
                        if (angle == 0)
                            StartSentence += "S";
                        else if (Math.Round(angle, 3) == Math.Round(Math.PI / 2, 3))
                            StartSentence += "R";
                        else if (Math.Round(angle, 3) == Math.Round(Math.PI / 2 * 3, 3))
                            StartSentence += "L";
                        else
                            StartSentence += "E";
                    }
                    else
                    {
                        StartSentence += "S#";
                    }
                }


                CC++;
                if (CC >= 1500)
                {
                    break;
                }
            }

            Sentence = StartSentence;
        }
        public static void FindPath(
            Curve StPath,
            Vector3d Sth1,
            Vector3d Stv2,
            double CellDim,
            ref List<Curve> CellResult) 
        {
            var Cell = ReorientedRec
        (
            StPath.PointAtStart,
            RectangleGet(CellDim, Stv2, Sth1)
        );
            var Cells = new List<Curve> { Cell.DuplicateCurve() };
            var ts = new List<double>();
            var tsStore = new DataTree<double>();
            double t = Intersection.CurveCurve(StPath, Cell, 0.2, 0.1).ToList()[0].ParameterA;
            int CC = 0;

            while (true)
            {
                if (ts.Contains(Math.Round(t, 4))) break;
                else
                {
                    var Tangent = StPath.TangentAt(t);
                    ts.Add(Math.Round(t, 4));
                    Tangent *= CellDim;
                    Cell.Translate(Tangent);
                    Cells.Add(Cell.DuplicateCurve());
                    var tsTemp = Intersection.CurveCurve(StPath, Cell, 0.1, 0.1).Select(x => x.ParameterA).ToList();
                    tsStore.AddRange(tsTemp, new GH_Path(CC));
                    t = tsTemp.Count == 1 ? tsTemp[0] : tsTemp.Last();

                    CC++;
                }
                if (CC >= 1500)
                {
                    break;
                }
            }
            CellResult = Cells;
        }
        public static Curve ReorientedRec(Point3d Pt, Curve Rec)
        {
            var DuRec = Rec.DuplicateCurve();
            DuRec.Translate(new Vector3d(Pt));
            return DuRec;
        }
        public static Curve RectangleGet(double DIM, Vector3d Vertical, Vector3d Horizonal)
         => new PolylineCurve(new Point3d[]{
            Point3d.Origin + Vertical * DIM / 2 + Horizonal * DIM / 2,
            Point3d.Origin + Vertical * DIM / 2 - Horizonal * DIM / 2,
            Point3d.Origin - Vertical * DIM / 2 - Horizonal * DIM / 2,
            Point3d.Origin - Vertical * DIM / 2 + Horizonal * DIM / 2,
            Point3d.Origin + Vertical * DIM / 2 + Horizonal * DIM / 2
        });
    }
}

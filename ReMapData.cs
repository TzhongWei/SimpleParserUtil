using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SimpleParserUtil
{
    public static class ReMapData
    {
        public static void Run(
        DataTree<Point3d> PtsSet,
        Vector3d UVVec,
        ref DataTree<Point3d> U_0,
        ref DataTree<Point3d> U_1,
        ref DataTree<bool> U_Pattern,
        ref DataTree<Point3d> V_0,
        ref DataTree<Point3d> V_1,
        ref DataTree<bool> V_Pattern
        )
        {
            var DirU_0 = new DataTree<Point3d>();
            var DirU_1 = new DataTree<Point3d>();
            var DirU_Pattern = new DataTree<bool>();

            var DirV_0 = new DataTree<Point3d>();
            var DirV_1 = new DataTree<Point3d>();
            var DirV_Pattern = new DataTree<bool>();

            FindPattern(PtsSet, UVec, ref DirU_0, ref DirU_1, ref DirU_Pattern);

            U_0 = DirU_0;
            U_1 = DirU_1;
            U_Pattern = DirU_Pattern;
            var PtsSetV = FlipPoints(PtsSet, UVec, out var SplitHint);
            var VVec = new Vector3d(UVec);
            VVec.Rotate(Math.PI * 0.5, Vector3d.ZAxis);
            FindPattern(PtsSetV, VVec, ref DirV_0, ref DirV_1, ref DirV_Pattern, true, SplitHint);
            V_0 = DirV_0;
            V_1 = DirV_1;
            V_Pattern = DirV_Pattern;
        }
        public static void FindPattern(
        DataTree<Point3d> PtsSet,
    Vector3d UVec,
    ref DataTree<Point3d> Dir_0,
    ref DataTree<Point3d> Dir_1,
    ref DataTree<bool> Dir_Pattern,
    bool Inverse = true,
    List<List<int>> SplitHint = null
    )
        {
            Dir_0 = new DataTree<Point3d>();
            Dir_1 = new DataTree<Point3d>();
            Dir_Pattern = new DataTree<bool>();

            double Angle = -Vector3d.VectorAngle(UVec, Vector3d.XAxis);
            var XArr = new List<double>();
            var YArr = new List<double>();
            var CheckPtList = new List<Point3d>();
            var PtList = new List<Point3d>();
            var RoTS = Transform.Rotation(Angle, Point3d.Origin);

            for (int i = 0; i < PtsSet.BranchCount; i++)
            {
                for (int j = 0; j < PtsSet.Branch(i).Count; j++)
                {
                    Point3d TempPt;

                    PtList.Add(TempPt = PtsSet.Branch(i)[j]);
                    TempPt.Transform(RoTS);
                    if (!XArr.Contains(Math.Round(TempPt.X, 3)))
                        XArr.Add(Math.Round(TempPt.X, 3));
                    if (!YArr.Contains(Math.Round(TempPt.Y, 3)))
                        YArr.Add(Math.Round(TempPt.Y, 3));
                    CheckPtList.Add(new Point3d(
                        Math.Round(TempPt.X, 3),
                        Math.Round(TempPt.Y, 3),
                        0
                    ));
                }
            }

            XArr.Sort();
            YArr.Sort();
            var Dist = 2 * (XArr[1] - XArr[0]);

            var ComparePt0 = new Point3d(PtsSet.Branch(0)[0]);
            var ComparePt1 = new Point3d(PtsSet.Branch(0)[1]);

            ComparePt0.Transform(RoTS);
            ComparePt1.Transform(RoTS);
            double Xvar, Yvar;
            if (Math.Abs(Xvar = ComparePt0.X - ComparePt1.X) > Math.Abs(Yvar = ComparePt0.Y - ComparePt1.Y))
            {
                if (Xvar > 0)
                    XArr.Reverse();
            }
            else
            {
                if (Yvar > 0)
                    YArr.Reverse();
            }

            for (int i = 0; i < XArr.Count; i++)
            {
                var PtDArrPattern_0 = new List<Point3d>();
                var PtDArrPattern_1 = new List<Point3d>();
                var BoolPattern = new List<bool>();
                for (int j = 0; j < YArr.Count; j++)
                {
                    bool Pattern = Inverse ? false : true;
                    if (i % 2 == 0 && j % 2 == 0)
                        Pattern = !Pattern;
                    else if (i % 2 == 1 && j % 2 == 1)
                        Pattern = !Pattern;


                    var TempPt = new Point3d(XArr[i], YArr[j], 0);
                    if (CheckPtList.Contains(TempPt))
                    {
                        var Index = Find(CheckPtList, TempPt);
                        if (Pattern)
                            PtDArrPattern_0.Add(PtList[Index]);
                        else
                            PtDArrPattern_1.Add(PtList[Index]);
                        BoolPattern.Add(Pattern);
                    }
                }
                Dir_0.AddRange(PtDArrPattern_0, new GH_Path(i));
                Dir_1.AddRange(PtDArrPattern_1, new GH_Path(i));
                Dir_Pattern.AddRange(BoolPattern, new GH_Path(i));
            }
            Dir_0 = SplitTree(Dir_0, Dist, out _);
            Dir_1 = SplitTree(Dir_1, Dist, out _);

            if (SplitHint != null)
                Dir_Pattern = SplitTree<bool>(Dir_Pattern, SplitHint);
        }
        public static DataTree<T> SplitTree<T>(DataTree<T> Trees, List<List<int>> Split)
        {
            var NewSplitTree = new DataTree<T>();
            int PathCount = 0;
            if (Trees.BranchCount != Split.Count) return Trees;

            for (int i = 0; i < Split.Count; i++)
            {
                if (Split[i].Count == 0)
                    NewSplitTree.AddRange(Trees.Branch(i), new GH_Path(PathCount));
                else
                {
                    foreach (var index in Split[i])
                    {
                        var TempList = new List<T>();
                        for (int k = 0; k < Trees.Branch(i).Count; k++)
                        {
                            if (k == index)
                            {
                                NewSplitTree.AddRange(TempList, new GH_Path(PathCount));
                                PathCount++;
                                TempList = new List<T>();
                                TempList.Add(Trees.Branch(i)[k]);
                            }
                            else
                                TempList.Add(Trees.Branch(i)[k]);
                        }
                        NewSplitTree.AddRange(TempList, new GH_Path(PathCount));
                    }
                }
                PathCount++;
            }
            return NewSplitTree;
        }
        public static DataTree<Point3d> FlipPoints(DataTree<Point3d> PtsSet, Vector3d UVec, out List<List<int>> SplitHint)
        {
            double Angle = -Vector3d.VectorAngle(UVec, Vector3d.XAxis);
            var XArr = new List<double>();
            var YArr = new List<double>();
            var CheckPtList = new List<Point3d>();
            var PtList = new List<Point3d>();
            for (int i = 0; i < PtsSet.BranchCount; i++)
            {
                for (int j = 0; j < PtsSet.Branch(i).Count; j++)
                {
                    var TempPt = PtsSet.Branch(i)[j];
                    PtList.Add(PtsSet.Branch(i)[j]);
                    var RoTS = Transform.Rotation(Angle, Point3d.Origin);
                    TempPt.Transform(RoTS);

                    if (!XArr.Contains(Math.Round(TempPt.X, 3)))
                        XArr.Add(Math.Round(TempPt.X, 3));
                    if (!YArr.Contains(Math.Round(TempPt.Y, 3)))
                        YArr.Add(Math.Round(TempPt.Y, 3));
                    CheckPtList.Add(new Point3d(
                        Math.Round(TempPt.X, 3),
                        Math.Round(TempPt.Y, 3),
                        0
                    ));

                }
            }
            XArr.Sort();
            YArr.Sort();
            var Distance = XArr[1] - XArr[0];

            //Flip tree into branches based on the Coordinate
            var NewTree = new DataTree<Point3d>();
            for (int i = 0; i < YArr.Count; i++)
            {
                var TempList = new List<Point3d>();
                for (int j = 0; j < XArr.Count; j++)
                {
                    if (CheckPtList.Contains(new Point3d(XArr[j], YArr[i], 0)))
                    {
                        var Index = Find(CheckPtList, new Point3d(XArr[j], YArr[i], 0));
                        TempList.Add(PtList[Index]);
                    }
                }
                NewTree.AddRange(TempList, new GH_Path(i));
            }

            //Check the continumm of the point list in the tree
            /*
            var NewSplitTree = new DataTree<Point3d>();
            int PathCount = 0;
            for(int i = 0; i < NewTree.BranchCount; i++ )
            {
                var PtListsTest = NewTree.Branch(i);
                List<int> SplitIndices = new List<int>();
                var TestPt = new Point3d(PtListsTest[0]);
                TestPt.Z = 0;
                for(int j = 1; j < PtListsTest.Count-1; j++)
                {
                    var TestPt2 = new Point3d(PtListsTest[j]);
                    TestPt2.Z = 0;
                    if(TestPt.DistanceTo(TestPt2) > Distance * 1.1) 
                        SplitIndices.Add(j); //<-split after j
                    TestPt = TestPt2;
                }

                if(SplitIndices.Count == 0)
                    NewSplitTree.AddRange(PtListsTest, new GH_Path(PathCount));
                else
                {

                    var TempList = new List<Point3d>();
                    foreach (var Index in SplitIndices)
                    {
                        for(int k = 0; k < PtListsTest.Count; k++)
                    {
                        if(k == SplitIndices[0]) 
                        {
                            NewSplitTree.AddRange(TempList, new GH_Path(PathCount));
                            PathCount++;
                            TempList = new List<Point3d>();
                        }
                        else
                            TempList.Add(PtListsTest[k]);
                    }
                    NewSplitTree.AddRange(TempList, new GH_Path(PathCount));
                    }

                }
                PathCount++;
            }
    */
            return SplitTree(NewTree, Distance, out SplitHint);
        }
        public static DataTree<Point3d> SplitTree(DataTree<Point3d> NewTree, double Distance, out List<List<int>> SplitArr)
        {
            var NewSplitTree = new DataTree<Point3d>();
            int PathCount = 0;
            SplitArr = new List<List<int>>();
            for (int i = 0; i < NewTree.BranchCount; i++)
            {
                if (NewTree.Branch(i).Count < 1) continue;
                var PtListsTest = NewTree.Branch(i);
                List<int> SplitIndices = new List<int>();
                var TestPt = new Point3d(PtListsTest[0]);
                TestPt.Z = 0;
                for (int j = 1; j < PtListsTest.Count; j++)
                {
                    var TestPt2 = new Point3d(PtListsTest[j]);
                    TestPt2.Z = 0;
                    if (TestPt.DistanceTo(TestPt2) > Distance * 1.1)
                        SplitIndices.Add(j); //<-split after j
                    TestPt = TestPt2;
                }
                SplitArr.Add(SplitIndices);
                if (SplitIndices.Count == 0)
                    NewSplitTree.AddRange(PtListsTest, new GH_Path(PathCount));
                else
                {

                    foreach (var Index in SplitIndices)
                    {
                        var TempList = new List<Point3d>();
                        for (int k = 0; k < PtListsTest.Count; k++)
                        {
                            if (k == Index)
                            {
                                NewSplitTree.AddRange(TempList, new GH_Path(PathCount));
                                PathCount++;
                                TempList = new List<Point3d>();
                                TempList.Add(PtListsTest[k]);
                            }
                            else
                                TempList.Add(PtListsTest[k]);
                        }
                        NewSplitTree.AddRange(TempList, new GH_Path(PathCount));
                    }

                }
                PathCount++;
            }
            return NewSplitTree;
        }
        public static int Find(IEnumerable<Point3d> AEnum, Point3d item)
        {
            var AList = AEnum.ToList();
            for (int i = 0; i < AList.Count; i++)
            {
                if (AList[i] == item)
                    return i;
            }
            return -1;
        }
    }
}

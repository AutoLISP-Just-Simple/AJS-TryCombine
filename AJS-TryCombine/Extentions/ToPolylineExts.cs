using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AJS_TryCombine
{
    static class ToPolylineExts
    {
        public static DBObjectCollection ToPolylines(this Region reg)
        {
            // We will return a collection of entities
            // (should include closed Polylines and other
            // closed curves, such as Circles)
            DBObjectCollection res = new DBObjectCollection();
            if (reg == null || reg.Area == 0) return res;
            // Explode Region -> collection of Curves / Regions
            DBObjectCollection cvs = new DBObjectCollection();
            reg.Explode(cvs);

            //foreach (DBObject d in cvs)
            //    if (d is Region r)
            //        cvs.AddRange(r.GetPolylines());
            // Create a plane to convert 3D coords
            // into Region coord system

            return cvs.TryCombine(false);
        }
        public static DBObjectCollection GetBoundaries(this Hatch hatch)
        {
            DBObjectCollection dbs = new DBObjectCollection();
            Plane plane = hatch.GetPlane();
            int nLoops = hatch.NumberOfLoops;
            for (int i = 0; i < nLoops; i++)
            {
                HatchLoop loop = hatch.GetLoopAt(i);
                //EDs.Princ("lop=" + loop.IsPolyline);
                if (loop.IsPolyline)
                {
                    Polyline poly = new Polyline();
                    int iVertex = 0;
                    foreach (BulgeVertex bv in loop.Polyline)
                    {
                        poly.AddVertexAt(iVertex++, bv.Vertex, bv.Bulge, 0.0, 0.0);
                    }
                    dbs.Add(poly);
                }
                else
                {
                    foreach (Curve2d cv in loop.Curves)
                    {
                        LineSegment2d line2d = cv as LineSegment2d;
                        CircularArc2d arc2d = cv as CircularArc2d;
                        EllipticalArc2d ellipse2d = cv as EllipticalArc2d;
                        NurbCurve2d spline2d = cv as NurbCurve2d;
                        if (line2d != null)
                        {
                            Line ent = new Line(line2d.StartPoint.To3d(), line2d.EndPoint.To3d());
                            dbs.Add(ent);
                        }
                        else if (arc2d != null)
                        {
                            if (arc2d.IsClosed() || Math.Abs(arc2d.EndAngle - arc2d.StartAngle) < 1e-5)
                            {
                                Circle ent = new Circle(new Point3d(plane, arc2d.Center), plane.Normal, arc2d.Radius);
                                dbs.Add(ent);
                            }
                            else
                            {
                                if (arc2d.IsClockWise)
                                {
                                    arc2d = arc2d.GetReverseParameterCurve() as CircularArc2d;
                                }
                                double angle = new Vector3d(plane, arc2d.ReferenceVector).AngleOnPlane(plane);
                                double startAngle = arc2d.StartAngle + angle;
                                double endAngle = arc2d.EndAngle + angle;
                                Arc ent = new Arc(new Point3d(plane, arc2d.Center), plane.Normal, arc2d.Radius, startAngle, endAngle);
                                dbs.Add(ent);
                            }
                        }
                        else if (ellipse2d != null)
                        {
                            //-------------------------------------------------------------------------------------------
                            // Bug: Can not assign StartParam and EndParam of Ellipse:
                            // Ellipse ent = new Ellipse(new Point3d(plane, e2d.Center), plane.Normal,
                            //      new Vector3d(plane,e2d.MajorAxis) * e2d.MajorRadius,
                            //      e2d.MinorRadius / e2d.MajorRadius, e2d.StartAngle, e2d.EndAngle);
                            // ent.StartParam = e2d.StartAngle;
                            // ent.EndParam = e2d.EndAngle;
                            // error CS0200: Property or indexer 'Autodesk.AutoCAD.DatabaseServices.Curve.StartParam' cannot be assigned to -- it is read only
                            // error CS0200: Property or indexer 'Autodesk.AutoCAD.DatabaseServices.Curve.EndParam' cannot be assigned to -- it is read only
                            //---------------------------------------------------------------------------------------------
                            // Workaround is using Reflection
                            //
                            Ellipse ent = new Ellipse(new Point3d(plane, ellipse2d.Center), plane.Normal,
                                 new Vector3d(plane, ellipse2d.MajorAxis) * ellipse2d.MajorRadius,
                                 ellipse2d.MinorRadius / ellipse2d.MajorRadius, ellipse2d.StartAngle, ellipse2d.EndAngle);

                            ent.GetType().InvokeMember("StartParam", BindingFlags.SetProperty, null,
                              ent, new object[] { ellipse2d.StartAngle });
                            ent.GetType().InvokeMember("EndParam", BindingFlags.SetProperty, null,
                              ent, new object[] { ellipse2d.EndAngle });

                            dbs.Add(ent);
                        }
                        else if (spline2d != null)
                        {
                            if (spline2d.HasFitData)
                            {
                                NurbCurve2dFitData n2fd = spline2d.FitData;
                                using (Point3dCollection p3ds = new Point3dCollection())
                                {
                                    foreach (Point2d p in n2fd.FitPoints) p3ds.Add(new Point3d(plane, p));
                                    Spline ent = new Spline(p3ds, new Vector3d(plane, n2fd.StartTangent), new Vector3d(plane, n2fd.EndTangent),
                                      /* n2fd.KnotParam, */  n2fd.Degree, n2fd.FitTolerance.EqualPoint);

                                    dbs.Add(ent);
                                }
                            }
                            else
                            {
                                NurbCurve2dData n2fd = spline2d.DefinitionData;
                                using (Point3dCollection p3ds = new Point3dCollection())
                                {
                                    DoubleCollection knots = new DoubleCollection(n2fd.Knots.Count);
                                    foreach (Point2d p in n2fd.ControlPoints) p3ds.Add(new Point3d(plane, p));
                                    foreach (double k in n2fd.Knots) knots.Add(k);
                                    Spline ent = new Spline(n2fd.Degree, n2fd.Rational,
                                        spline2d.IsClosed(), spline2d.IsPeriodic(out double period),
                                        p3ds, knots, n2fd.Weights, n2fd.Knots.Tolerance, n2fd.Knots.Tolerance);

                                    dbs.Add(ent);
                                }
                            }
                        }
                    }
                    dbs = dbs.TryCombine();
                }
            }

            //EDs.Princ("ABCDFD fsd fsd fdsf sdf");
            foreach (DBObject d in dbs)
            {
                //EDs.Princ("FromHatch:" + d.GetType());
                Entity e = d as Entity;
                try
                {
                    e.Layer = hatch.Layer;
                    e.ColorIndex = hatch.ColorIndex;
                }
                catch { }
            }
            return dbs;
        }

        public static Point3d To3d(this Point2d p)
            => new Point3d(p.X, p.Y, 0);

    }
}

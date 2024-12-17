using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AJS_TryCombine
{
    static class TryCombineExts
    {
        public static DBObjectCollection TryCombine(this DBObjectCollection cvs, bool samelayer = false, double tol = 1e-5)
        {
            DBObjectCollection res = new DBObjectCollection();
            if (cvs.Count < 2) return cvs;
            // Explode Region -> collection of Curves / Regions

            // Create a plane to convert 3D coords
            // into Region coord system

            bool finished = false;
            while (!finished && cvs.Count > 0)
            {
                // Count the Curves and the non-Curves, and find
                // the index of the first Curve in the collection
                int cvCnt = 0, nonCvCnt = 0, fstCvIdx = -1;
                for (int i = 0; i < cvs.Count; i++)
                {
                    Curve tmpCv = cvs[i] as Curve;
                    if (tmpCv == null)
                        nonCvCnt++;
                    else
                    {
                        if (tmpCv.Closed || tmpCv.StartPoint.DistanceTo(tmpCv.EndPoint) < tol)
                        {
                            res.Add(tmpCv);
                            cvs.Remove(tmpCv);
                            // Decrement, so we don't miss an item
                            i--;
                        }
                        else
                        {
                            cvCnt++;
                            if (fstCvIdx == -1) fstCvIdx = i;
                        }
                    }
                }
                if (fstCvIdx >= 0)
                {
                    // For the initial segment take the first
                    // Curve in the collection
                    Curve fstCv = (Curve)cvs[fstCvIdx];
                    // The resulting Polyline
                    Polyline p = new Polyline();
                    p.XData = fstCv.XData;
                    //fstCv.CopyPropertiesTo(p);
                    // Set common entity properties from the Region
                    // Add the first two vertices, but only set the
                    // bulge on the first (the second will be set
                    // retroactively from the second segment)
                    // We also assume the first segment is counter-
                    // clockwise (the default for arcs), as we're
                    // not swapping the order of the vertices to
                    // make them fit the Polyline's order
                    //EDs.Princ(fstCv.StartPoint.ToString() + " d2=" + fstCv.StartPoint.To2d());
                    p.AddVertexAt(p.NumberOfVertices, fstCv.StartPoint.To2d(), BulgeFromCurve(fstCv, false), 0, 0);
                    p.AddVertexAt(p.NumberOfVertices, fstCv.EndPoint.To2d(), 0, 0, 0);
                    cvs.Remove(fstCv);
                    // The next point to look for
                    Point3d nextPt = fstCv.EndPoint;
                    // We no longer need the curve
                    //try
                    {
                        if (fstCv.Layer != "" && fstCv.LayerId != ObjectId.Null) p.LayerId = fstCv.LayerId;
                        if (fstCv.ColorIndex >= 0 && fstCv.ColorIndex <= 256) p.ColorIndex = fstCv.ColorIndex;
                        p.LineWeight = fstCv.LineWeight;
                    }
                    //catch { }

                    //fstCv.Dispose();
                    // Find the line that is connected to
                    // the next point
                    // If for some reason the lines returned were not
                    // connected, we could loop endlessly.
                    // So we store the previous curve count and assume
                    // that if this count has not been decreased by
                    // looping completely through the segments once,
                    // then we should not continue to loop.
                    // Hopefully this will never happen, as the curves
                    // should form a closed loop, but anyway...
                    // Set the previous count as artificially high,
                    // so that we loop once, at least.
                    int prevCnt = cvs.Count + 1;
                    while (cvs.Count > nonCvCnt && cvs.Count < prevCnt)
                    {
                        prevCnt = cvs.Count;
                        foreach (DBObject obj in cvs)
                        {
                            Curve cv = obj as Curve;
                            if (cv != null && (!samelayer || cv.Layer == p.Layer))
                            {
                                // If one end of the curve connects with the
                                // point we're looking for...
                                if (cv.StartPoint.Equal(nextPt, tol) || cv.EndPoint.Equal(nextPt, tol))
                                {
                                    // Calculate the bulge for the curve and
                                    // set it on the previous vertex
                                    double bulge = BulgeFromCurve(cv, cv.EndPoint.Equal(nextPt, tol));
                                    if (bulge != 0.0) p.SetBulgeAt(p.NumberOfVertices - 1, bulge);
                                    // Reverse the points, if needed
                                    if (cv.StartPoint.Equal(nextPt, tol))
                                        nextPt = cv.EndPoint;
                                    else
                                        // cv.EndPoint == nextPt
                                        nextPt = cv.StartPoint;
                                    // Add out new vertex (bulge will be set next
                                    // time through, as needed)
                                    p.AddVertexAt(p.NumberOfVertices, nextPt.To2d(), 0, 0, 0);
                                    // Remove our curve from the list, which
                                    // decrements the count, of course
                                    cvs.Remove(cv);
                                    //cv.Dispose();
                                    break;
                                }
                            }
                        }
                    }

                    p.ReverseCurve();
                    nextPt = p.EndPoint;
                    // We no longer need the curve
                    // Find the line that is connected to
                    // the next point
                    // If for some reason the lines returned were not
                    // connected, we could loop endlessly.
                    // So we store the previous curve count and assume
                    // that if this count has not been decreased by
                    // looping completely through the segments once,
                    // then we should not continue to loop.
                    // Hopefully this will never happen, as the curves
                    // should form a closed loop, but anyway...
                    // Set the previous count as artificially high,
                    // so that we loop once, at least.
                    prevCnt = cvs.Count + 1;
                    while (cvs.Count > nonCvCnt && cvs.Count < prevCnt)
                    {
                        prevCnt = cvs.Count;
                        foreach (DBObject obj in cvs)
                        {
                            Curve cv = obj as Curve;
                            if (cv != null && (!samelayer || cv.Layer == p.Layer))
                            {
                                // If one end of the curve connects with the
                                // point we're looking for...
                                if (cv.StartPoint.Equal(nextPt, tol) || cv.EndPoint.Equal(nextPt, tol))
                                {
                                    // Calculate the bulge for the curve and
                                    // set it on the previous vertex
                                    double bulge = BulgeFromCurve(cv, cv.EndPoint.Equal(nextPt, tol));
                                    if (bulge != 0.0) p.SetBulgeAt(p.NumberOfVertices - 1, bulge);
                                    // Reverse the points, if needed
                                    if (cv.StartPoint.Equal(nextPt, tol))
                                        nextPt = cv.EndPoint;
                                    else
                                        // cv.EndPoint == nextPt
                                        nextPt = cv.StartPoint;
                                    // Add out new vertex (bulge will be set next
                                    // time through, as needed)
                                    p.AddVertexAt(p.NumberOfVertices, nextPt.To2d(), 0, 0, 0);
                                    // Remove our curve from the list, which
                                    // decrements the count, of course
                                    cvs.Remove(cv);
                                    //cv.Dispose();
                                    break;
                                }
                            }
                        }
                    }

                    // Once we have added all the Polyline's vertices,
                    // transform it to the original region's plane

                    //p.TransformBy(Matrix3d.PlaneToWorld(pl));
                    p.ReverseCurve();

                    res.Add(p);
                    if (cvs.Count == nonCvCnt) finished = true;
                }
                // If there are any Regions in the collection,
                // recurse to explode and add their geometry
                if (nonCvCnt > 0 && cvs.Count > 0)
                {
                    foreach (DBObject obj in cvs)
                    {
                        Region subReg = obj as Region;
                        if (subReg != null)
                        {
                            foreach (Entity e in subReg.ToPolylines())
                                res.Add(e);

                            cvs.Remove(subReg);
                            //subReg.Dispose();
                        }
                    }
                }
                if (cvs.Count == 0) finished = true;
            }

            return res;
        }

        public static Point2d To2d(this Point3d p)
        {
            return new Point2d(p.X, p.Y);
        }

        public static bool Equal(this Point3d pt, Point3d thatpt, double tol = 0.000001)
            => pt.DistanceTo(thatpt) < tol;

        public static double BulgeFromCurve(this Curve cv, bool clockwise)
        {
            double bulge = 0.0;
            Arc a = cv as Arc;
            if (a != null)
            {
                double newStart;
                // The start angle is usually greater than the end,
                // as arcs are all counter-clockwise.
                // (If it isn't it's because the arc crosses the
                // 0-degree line, and we can subtract 2PI from the
                // start angle.)
                if (a.StartAngle > a.EndAngle)
                    newStart = a.StartAngle - 8 * Math.Atan(1);
                else
                    newStart = a.StartAngle;
                // Bulge is defined as the tan of
                // one fourth of the included angle
                bulge = Math.Tan((a.EndAngle - newStart) / 4);
                // If the curve is clockwise, we negate the bulge
                if (clockwise) bulge = -bulge;
            }
            return bulge;
        }
    }
}

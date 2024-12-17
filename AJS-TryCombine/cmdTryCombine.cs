// (C) Copyright 2024 by
//
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System.Linq;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(AJS_TryCombine.MyCommands))]

namespace AJS_TryCombine
{
    // This class is instantiated by AutoCAD for each document when
    // a command is called by the user the first time in the context
    // of a given document. In other words, non static data in this class
    // is implicitly per-document!
    public class MyCommands
    {
        // Modal Command with localized name
        [CommandMethod("TryCombine", CommandFlags.Modal)]
        public void MyCommand_TryCombine() // This method can have any name
        {
            // Put your command code here
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                var ed = doc.Editor;
                ed.WriteMessage("Hello, this is your first command. Create by www.lisp.vn");
                var psr = ed.GetSelection(new SelectionFilter(new TypedValue[] { new TypedValue(0, "*line,arc") }));
                if (psr.Status != PromptStatus.OK) return;

                var ids = psr.Value.GetObjectIds().ToList();

                using (var tr = HostApplicationServices.WorkingDatabase.TransactionManager.StartTransaction())
                {
                    var ents = ids.Select(x => tr.GetObject(x, OpenMode.ForWrite, false, true) as Entity).ToList();
                    var cvs = new DBObjectCollection();
                    foreach (var ent in ents)
                    {
                        if (ent is Arc || ent is Line)
                            cvs.Add(ent.Clone() as Curve);
                        else
                        {
                            var dbs = new DBObjectCollection();
                            ent.Explode(dbs);
                            foreach (Entity e in dbs)
                                if (e is Arc || e is Line)
                                    cvs.Add(e as Curve);
                        }
                    }

                    var btr = tr.GetObject(HostApplicationServices.WorkingDatabase.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                    var pls = cvs.TryCombine();
                    foreach (Curve pl in pls)
                    {
                        btr.AppendEntity(pl);
                        tr.AddNewlyCreatedDBObject(pl, true);
                    }

                    tr.Commit();
                }
            }
        }
    }
}
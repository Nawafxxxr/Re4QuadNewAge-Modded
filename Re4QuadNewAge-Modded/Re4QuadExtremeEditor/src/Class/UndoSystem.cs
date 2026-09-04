using System;
using System.Collections.Generic;
using OpenTK;
using Re4QuadExtremeEditor.src.Class.TreeNodeObj;
using Re4QuadExtremeEditor.src.Class.Files;

namespace Re4QuadExtremeEditor.src.Class
{
    /// <summary>
    /// Lightweight command stack powering Ctrl+Z / Ctrl+Y.
    /// Commands capture enough state to be reversible without ever
    /// touching file-line internals, so an undo can never corrupt data:
    /// - MoveCommand: gizmo drags (camera-space positions before/after).
    /// - AddCommand : duplicated objects (tree removal is always possible).
    /// </summary>
    public static class UndoSystem
    {
        private const int MaxEntries = 128;

        private static readonly List<IUndoCommand> undoStack = new List<IUndoCommand>();
        private static readonly List<IUndoCommand> redoStack = new List<IUndoCommand>();

        /// <summary>Set by MainForm; shows the "Undone: ..." / "Redone: ..." toast.</summary>
        public static Action<string> Notify = null;

        public interface IUndoCommand
        {
            string Label { get; }
            /// <summary>False for commands whose redo cannot faithfully restore state (e.g. added file lines).</summary>
            bool Redoable { get; }
            void Undo();
            void Redo();
        }

        public static void Push(IUndoCommand command)
        {
            if (command == null) return;
            undoStack.Add(command);
            if (undoStack.Count > MaxEntries) undoStack.RemoveAt(0);
            redoStack.Clear();
        }

        public static bool CanUndo { get { return undoStack.Count > 0; } }
        public static bool CanRedo { get { return redoStack.Count > 0; } }

        public static void Undo()
        {
            if (undoStack.Count == 0)
            {
                if (Notify != null) Notify("Nothing to undo");
                return;
            }
            IUndoCommand cmd = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            cmd.Undo();
            if (cmd.Redoable) redoStack.Add(cmd);
            if (Notify != null) Notify("Undone: " + cmd.Label);
        }

        public static void Redo()
        {
            if (redoStack.Count == 0)
            {
                if (Notify != null) Notify("Nothing to redo");
                return;
            }
            IUndoCommand cmd = redoStack[redoStack.Count - 1];
            redoStack.RemoveAt(redoStack.Count - 1);
            cmd.Redo();
            undoStack.Add(cmd);
            if (Notify != null) Notify("Redone: " + cmd.Label);
        }

        public static void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
        }

        // ------------------------------------------------------------------

        private sealed class MoveCommand : IUndoCommand
        {
            private readonly Object3D[] objects;
            private readonly Vector3[] from;
            private readonly Vector3[] to;

            public MoveCommand(List<Object3D> objs, List<Vector3> start, Vector3[] end)
            {
                objects = objs.ToArray();
                from = start.ToArray();
                to = end;
            }

            public string Label
            {
                get { return objects.Length == 1 ? "move " + objects[0].ObjLineRef : "move " + objects.Length + " objects"; }
            }

            public bool Redoable { get { return true; } }

            public void Undo() { Apply(from); }
            public void Redo() { Apply(to); }

            private void Apply(Vector3[] positions)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    if (objects[i] != null && objects[i].Parent != null)
                    {
                        objects[i].SetObjPosition_ToCamera(positions[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Records a completed gizmo drag. Call once from EndDrag with the
        /// captured object list and their starting camera-space positions.
        /// The command is skipped when nothing actually moved.
        /// </summary>
        public static void PushMove(List<Object3D> objects, List<Vector3> startPositions, Func<Vector3[]> currentPositionGetter, string label = null)
        {
            if (objects == null || objects.Count == 0 || currentPositionGetter == null) return;
            Vector3[] end = currentPositionGetter();
            if (end == null || end.Length != objects.Count) return;

            bool anyMoved = false;
            for (int i = 0; i < objects.Count; i++)
            {
                Vector3 d = end[i] - startPositions[i];
                if (Math.Abs(d.X) > 1e-5f || Math.Abs(d.Y) > 1e-5f || Math.Abs(d.Z) > 1e-5f)
                {
                    anyMoved = true;
                    break;
                }
            }
            if (!anyMoved) return;

            Push(new MoveCommand(objects, startPositions, end));
        }

        /// <summary>
        /// Records a completed panel gesture using the FULL move-general state
        /// (position + rotation + scale + trigger-zone points). Covers every
        /// transformation the object control panel can perform, including
        /// rotation-only and scale-only changes that leave the origin fixed.
        /// The command is skipped when nothing actually changed.
        /// </summary>
        public static void PushGeneral(List<Object3D> objects, List<Vector3[]> startStates, Func<Vector3[][]> currentStateGetter, string label = null)
        {
            if (objects == null || objects.Count == 0 || startStates == null || currentStateGetter == null) return;
            Vector3[][] end = currentStateGetter();
            if (end == null || end.Length != objects.Count) return;

            bool anyChanged = false;
            for (int i = 0; i < objects.Count; i++)
            {
                Vector3[] a = startStates[i];
                Vector3[] b = end[i];
                if (a == null || b == null || a.Length != b.Length) { anyChanged = true; break; }
                for (int j = 0; j < a.Length; j++)
                {
                    if (Math.Abs(a[j].X - b[j].X) > 1e-5f ||
                        Math.Abs(a[j].Y - b[j].Y) > 1e-5f ||
                        Math.Abs(a[j].Z - b[j].Z) > 1e-5f)
                    {
                        anyChanged = true;
                        break;
                    }
                }
                if (anyChanged) break;
            }
            if (!anyChanged) return;

            Push(new GeneralCommand(objects, new List<Vector3[]>(startStates), end));
        }

        /// <summary>Complete reversible snapshot of one object: position +
        /// rotation angles + scale. Any component the object type does not
        /// support stays null and is skipped on restore.</summary>
        public sealed class FullTransformState
        {
            public Vector3[] Position;
            public Vector3[] Rotation;
            public Vector3[] Scale;
        }

        public static FullTransformState CaptureFullTransform(Object3D obj)
        {
            FullTransformState st = new FullTransformState();
            try { st.Position = obj.GetObjPostion_ToMove_General(); } catch { }
            try { st.Rotation = obj.GetObjRotarionAngles_ToMove(); } catch { }
            try { st.Scale = obj.GetObjScale_ToMove(); } catch { }
            return st;
        }

        private static bool ArrayChanged(Vector3[] a, Vector3[] b)
        {
            if (a == null || b == null) return !(a == null && b == null);
            if (a.Length != b.Length) return true;
            for (int i = 0; i < a.Length; i++)
            {
                if (Math.Abs(a[i].X - b[i].X) > 1e-5f ||
                    Math.Abs(a[i].Y - b[i].Y) > 1e-5f ||
                    Math.Abs(a[i].Z - b[i].Z) > 1e-5f) return true;
            }
            return false;
        }

        private static bool StateChanged(FullTransformState a, FullTransformState b)
        {
            if (a == null || b == null) return true;
            return ArrayChanged(a.Position, b.Position)
                || ArrayChanged(a.Rotation, b.Rotation)
                || ArrayChanged(a.Scale, b.Scale);
        }

        private static void ApplyState(Object3D obj, FullTransformState s)
        {
            if (obj == null || obj.Parent == null || s == null) return;
            try { if (s.Position != null) obj.SetObjPostion_ToMove_General(s.Position); } catch { }
            try { if (s.Rotation != null) obj.SetObjRotarionAngles_ToMove(s.Rotation); } catch { }
            try { if (s.Scale != null) obj.SetObjScale_ToMove(s.Scale); } catch { }
        }

        private sealed class FullTransformCommand : IUndoCommand
        {
            private readonly Object3D[] objects;
            private readonly FullTransformState[] from;
            private readonly FullTransformState[] to;

            public FullTransformCommand(List<Object3D> objs, List<FullTransformState> start, FullTransformState[] end)
            {
                objects = objs.ToArray();
                from = start.ToArray();
                to = end;
            }

            public string Label
            {
                get { return objects.Length == 1 ? "transform " + objects[0].ObjLineRef : "transform " + objects.Length + " objects"; }
            }

            public bool Redoable { get { return true; } }

            public void Undo() { for (int i = 0; i < objects.Length; i++) ApplyState(objects[i], from[i]); }
            public void Redo() { for (int i = 0; i < objects.Length; i++) ApplyState(objects[i], to[i]); }
        }

        /// <summary>
        /// Records a completed gesture using position + rotation + scale.
        /// Works for every object type (enemies rotate via their angle fields,
        /// trigger zones resize via their points array, etc.). The command is
        /// skipped when nothing actually changed.
        /// </summary>
        public static void PushFullTransform(List<Object3D> objects, List<FullTransformState> startStates, Func<FullTransformState[]> currentStateGetter, string label = null)
        {
            if (objects == null || objects.Count == 0 || startStates == null || currentStateGetter == null) return;
            FullTransformState[] end = currentStateGetter();
            if (end == null || end.Length != objects.Count) return;

            bool anyChanged = false;
            for (int i = 0; i < objects.Count; i++)
            {
                if (StateChanged(startStates[i], end[i])) { anyChanged = true; break; }
            }
            if (!anyChanged) return;

            Push(new FullTransformCommand(objects, new List<FullTransformState>(startStates), end));
        }

        private sealed class GeneralCommand : IUndoCommand
        {
            private readonly Object3D[] objects;
            private readonly Vector3[][] from;
            private readonly Vector3[][] to;

            public GeneralCommand(List<Object3D> objs, List<Vector3[]> start, Vector3[][] end)
            {
                objects = objs.ToArray();
                from = start.ToArray();
                to = end;
            }

            public string Label
            {
                get { return objects.Length == 1 ? "transform " + objects[0].ObjLineRef : "transform " + objects.Length + " objects"; }
            }

            public bool Redoable { get { return true; } }

            public void Undo() { Apply(from); }
            public void Redo() { Apply(to); }

            private void Apply(Vector3[][] states)
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    if (objects[i] != null && objects[i].Parent != null && states[i] != null)
                    {
                        objects[i].SetObjPostion_ToMove_General(states[i]);
                    }
                }
            }
        }

        // ------------------------------------------------------------------

        private sealed class AddCommand : IUndoCommand
        {
            private readonly struct Entry
            {
                public readonly Object3D Node;
                public readonly int Index;
                public Entry(Object3D node, int index) { Node = node; Index = index; }
            }

            private readonly Entry[] entries;
            private readonly string groupLabel;

            public AddCommand(List<object> nodesAndIndexes, string label)
            {
                var list = new List<Entry>();
                for (int i = 0; i + 1 < nodesAndIndexes.Count; i += 2)
                {
                    list.Add(new Entry((Object3D)nodesAndIndexes[i], (int)nodesAndIndexes[i + 1]));
                }
                entries = list.ToArray();
                groupLabel = label;
            }

            public string Label { get { return groupLabel; } }

            public bool Redoable { get { return false; } }

            public void Undo()
            {
                // remove newest-first so stored indexes stay valid
                for (int i = entries.Length - 1; i >= 0; i--)
                {
                    Object3D node = entries[i].Node;
                    if (node == null || node.Parent == null) continue;
                    var parent = node.Parent;
                    node.Remove();
                    var change = parent as Re4QuadExtremeEditor.src.Class.Interfaces.INodeChangeAmount;
                    if (change != null) change.ChangeAmountMethods.RemoveLineID(node.ObjLineRef);
                }
            }

            public void Redo() { }
        }

        /// <summary>Records objects created by DuplicateSelection for clean undo.</summary>
        public static void PushAdd(List<Object3D> createdNodes, string label)
        {
            if (createdNodes == null || createdNodes.Count == 0) return;
            var flat = new List<object>();
            foreach (Object3D n in createdNodes)
            {
                if (n == null) continue;
                flat.Add(n);
                flat.Add(n.Index);
            }
            Push(new AddCommand(flat, label));
        }

        // ------------------------------------------------------------------
        // RTP undo: snapshot-based, covers delete / link / unlink / add

        private sealed class RtpCommand : IUndoCommand
        {
            private readonly File_RTP_Group file;
            private readonly RtpSnapshot before;
            private readonly RtpSnapshot after;
            private readonly string label;

            public RtpCommand(File_RTP_Group f, RtpSnapshot b, RtpSnapshot a, string l)
            {
                file = f; before = b; after = a; label = l;
            }

            public string Label { get { return label; } }
            public bool Redoable { get { return true; } }

            public void Undo()
            {
                if (file == null || before == null) return;
                file.RestoreSnapshot(before);
            }

            public void Redo()
            {
                if (file == null || after == null) return;
                file.RestoreSnapshot(after);
            }
        }

        public static void PushRtp(File_RTP_Group file, RtpSnapshot before, RtpSnapshot after, string label)
        {
            if (file == null || before == null || after == null) return;
            Push(new RtpCommand(file, before, after, label));
        }
    }
}

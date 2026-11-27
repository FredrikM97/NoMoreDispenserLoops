global using Tag = System.Int32;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
    internal sealed class CodeInstruction
    {
        internal OpCode opcode;
        internal object operand;
        internal List<Label> labels = new();
        internal List<object> blocks = new();
        internal CodeInstruction(OpCode opcode, object operand = null)
        {
            this.opcode = opcode;
            this.operand = operand;
        }
    }

    internal static class CodeInstructionExtensions
    {
        internal static CodeInstruction MoveLabelsTo(this CodeInstruction instruction, CodeInstruction other)
        {
            other.labels.AddRange(instruction.labels);
            instruction.labels.Clear();
            return instruction;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type, string name) { }
    }

    internal static class AccessTools
    {
        public delegate ref F FieldRef<T, F>(T instance);

        public static FieldRef<T, F> FieldRefAccess<T, F>(string name)
        {
            object accessor = name switch
            {
                "pickups" => new FieldRef<FetchManager, List<FetchManager.Pickup>>(FetchManager.PickupsRef),
                "pickupsWhichCanBePickedUp" => new FieldRef<FetchManager.FetchablesByPrefabId, List<FetchManager.Pickup>>(FetchManager.FetchablesByPrefabId.EligibleRef),
                "cellCosts" => new FieldRef<FetchManager.FetchablesByPrefabId, Dictionary<int, int>>(FetchManager.FetchablesByPrefabId.CostsRef),
                _ => throw new MissingFieldException(name)
            };
            return (FieldRef<T, F>)accessor;
        }
    }
}

namespace UnityEngine
{
    public class Component
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
    }
    public sealed class Transform
    {
        public Vector3 position;
        public void SetPosition(Vector3 value) => position = value;
    }
    public struct Vector3 { public int Cell; }
    public static class Object
    {
        public static void Destroy(GameObject obj) { }
    }
    public sealed class GameObject
    {
        public GameObject(string name = null) { }
        public Transform transform = new();
        public int Cell;
        public Storage Storage;
        public ObjectDispenser Dispenser;
        public Rotatable Rotatable;
        public Prioritizable Prioritizable;
        public TreeFilterable Filterable = new();
        public T AddOrGet<T>() where T : new() => new T();
        public T AddComponent<T>() where T : Component, new() => new T { gameObject = this };
    }
}

namespace KSerialization
{
    public enum MemberSerialization { OptIn }
    public sealed class SerializationConfigAttribute : Attribute
    {
        public SerializationConfigAttribute(MemberSerialization value) { }
    }
    public sealed class SerializeAttribute : Attribute { }
}

public interface ISaveLoadable { }
public interface ICheckboxControl
{
    string CheckboxTitleKey { get; }
    string CheckboxLabel { get; }
    string CheckboxTooltip { get; }
    bool GetCheckboxValue();
    void SetCheckboxValue(bool value);
}
public class KMonoBehaviour : UnityEngine.Component
{
    private readonly Dictionary<int, Action<object>> callbacks = new Dictionary<int, Action<object>>();
    public T GetComponent<T>() where T : class
    {
        if (typeof(T) == typeof(Storage))
            return gameObject.Storage as T;
        if (typeof(T) == typeof(ObjectDispenser))
            return gameObject.Dispenser as T;
        if (typeof(T) == typeof(Rotatable))
            return gameObject.Rotatable as T;
        if (typeof(T) == typeof(Prioritizable))
            return gameObject.Prioritizable as T;
        if (typeof(T) == typeof(TreeFilterable))
            return gameObject.Filterable as T;
        return null;
    }
    protected void Subscribe(int hash, Action<object> callback) => callbacks.Add(hash, callback);
    public void Trigger(int hash) => callbacks[hash](null);
    protected virtual void OnPrefabInit() { }
    protected virtual void OnSpawn() { }
    protected virtual void OnCleanUp() { }
    public void TestPrefabInit() => OnPrefabInit();
    public void TestSpawn() => OnSpawn();
    public void TestCleanUp() => OnCleanUp();
}
public enum GameHashes { Rotated = -1643076535 }
public struct CellOffset { public int Value; }
public sealed class ObjectDispenser { public CellOffset dropOffset; }
public sealed class TreeFilterable
{
    public HashSet<int> AcceptedTags = new() { 0 };
    public bool ContainsTag(int tag) => AcceptedTags.Contains(tag);
}
public sealed class KPrefabID { public int PrefabTag; }
public struct PrioritySetting : IEquatable<PrioritySetting>
{
    public int Class;
    public int Value;
    public int CompareTo(PrioritySetting other) =>
        Class != other.Class ? Class.CompareTo(other.Class) : Value.CompareTo(other.Value);
    public bool Equals(PrioritySetting other) => Class == other.Class && Value == other.Value;
    public override bool Equals(object obj) => obj is PrioritySetting other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Class, Value);
    public static bool operator <=(PrioritySetting left, PrioritySetting right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PrioritySetting left, PrioritySetting right) => left.CompareTo(right) >= 0;
    public static bool operator ==(PrioritySetting left, PrioritySetting right) => left.Equals(right);
    public static bool operator !=(PrioritySetting left, PrioritySetting right) => !left.Equals(right);
}
public sealed class Prioritizable
{
    public PrioritySetting Priority = new() { Value = 5 };
    public int Reads;
    public PrioritySetting GetMasterPriority()
    {
        Reads++;
        return Priority;
    }
}
public sealed class Rotatable
{
    public bool Flipped;
    public CellOffset GetRotatedCellOffset(CellOffset offset) =>
        new CellOffset { Value = Flipped ? -offset.Value : offset.Value };
}
public sealed class ObjectDispenserConfig { }
public static class Grid
{
    public enum SceneLayer { Ore }
    public const int InvalidCell = -1;
    public static int CellCount = 100;
    public static bool[] Solid = new bool[CellCount];
    public static byte[] WorldIdx = new byte[CellCount];
    public static int CellBelowCalls;
    public static int PosToCell(UnityEngine.GameObject go) => go.Cell;
    public static int OffsetCell(int cell, CellOffset offset) => cell + offset.Value;
    public static int CellBelow(int cell)
    {
        CellBelowCalls++;
        return cell - 10;
    }
    public static bool IsValidCell(int cell) => cell >= 0 && cell < CellCount;
    public static bool IsSolidCell(int cell) => IsValidCell(cell) && Solid[cell];
    public static void CellToXY(int cell, out int x, out int y) { x = cell % 10; y = cell / 10; }
    public static UnityEngine.Vector3 CellToPosCCC(int cell, SceneLayer layer) => new() { Cell = cell };
}
public struct Vector2I
{
    public Vector2I(int x, int y) { }
}
public sealed class RangeVisualizer : UnityEngine.Component
{
    public Vector2I RangeMin;
    public Vector2I RangeMax;
    public bool TestLineOfSight;
    public Func<int, bool> BlockingCb;
}
public static class RangeVisualizerEffect
{
    public static readonly HashSet<RangeVisualizer> Active = new();
    public static void AddExtraRangeVisualizer(RangeVisualizer visualizer) => Active.Add(visualizer);
    public static void RemoveExtraRangeVisualizer(RangeVisualizer visualizer) => Active.Remove(visualizer);
}
public sealed class KSelectable
{
    public NoMoreDispenserLoopsComponent Component;
    public T GetComponent<T>() where T : class => Component as T;
}
public sealed class SelectTool
{
    public KSelectable selected;
    public void Select(KSelectable selection) => selected = selection;
}
public sealed class HandleVector<T>
{
    public struct Handle
    {
        internal int Id;
        public bool IsValid() => Id != 0;
    }
}
public sealed class GameScenePartitioner
{
    private readonly Dictionary<int, (int X, int Y, int Height, Action<object, object> Callback, object Context)> entries = new();
    private int nextId;
    public int AddCount;
    public int FreeCount;
    public static GameScenePartitioner Instance = new();
    public object solidChangedLayer = new();
    public HandleVector<int>.Handle Add(string name, object obj, int x, int y, int width, int height,
        object layer, Action<object, object> callback, object context)
    {
        AddCount++;
        int id = ++nextId;
        entries.Add(id, (x, y, height, callback, context));
        return new HandleVector<int>.Handle { Id = id };
    }
    public void Free(ref HandleVector<int>.Handle handle)
    {
        FreeCount++;
        entries.Remove(handle.Id);
        handle = default;
    }
    public void ChangeSolid(int cell, bool solid)
    {
        Grid.Solid[cell] = solid;
        Grid.CellToXY(cell, out int x, out int y);
        foreach (var entry in entries.Values.ToArray())
            if (entry.X == x && y >= entry.Y && y < entry.Y + entry.Height)
                entry.Callback(entry.Context, null);
    }
}
public sealed class Game
{
    public static Game Instance = new Game();
    public FetchManager fetchManager;
}
public sealed class Storage
{
    public int Id;
    public int GetInstanceID() => Id;
}
public sealed class Pickupable
{
    public int cachedCell;
    public Storage storage;
    public bool Allowed = true;
    public KPrefabID KPrefabID = new();
}
public sealed class ChoreConsumerState
{
    public object resume;
    public bool hasSolidTransferArm;
}
public class Chore
{
    public sealed class Precondition
    {
        public delegate bool Check(ref Context context, object data);
        public Check fn;
        public struct Context
        {
            public Chore chore;
            public object data;
            public ChoreConsumerState consumerState;
        }
    }
}
public sealed class FetchChore : Chore
{
    public Storage destination;
    public object overrideTarget;
    public object driver;
    public int tagsHash;
    public int requiredTag;
    public int forbidHash;
    public static readonly Precondition IsFetchTargetAvailable = new Precondition { fn = Check };
    private static bool Check(ref Precondition.Context context, object data) => true;
    public Pickupable FindFetchTarget(ChoreConsumerState consumer_state) => null;
}
public sealed class FetchManager
{
    public struct Pickup
    {
        public Pickupable pickupable;
        public int tagBitsHash;
        public ushort PathCost;
        public int masterPriority;
        public int foodQuality;
        public int freshness;
    }
    public sealed class FetchablesByPrefabId
    {
        public List<Pickup> finalPickups = new List<Pickup>();
        public List<Pickup> eligible = new List<Pickup>();
        public Dictionary<int, int> costs = new Dictionary<int, int>();
        public static ref List<Pickup> EligibleRef(FetchablesByPrefabId group) => ref group.eligible;
        public static ref Dictionary<int, int> CostsRef(FetchablesByPrefabId group) => ref group.costs;
    }
    public List<Pickup> pickups = new List<Pickup>();
    public Dictionary<int, FetchablesByPrefabId> prefabIdToFetchables = new Dictionary<int, FetchablesByPrefabId>();
    public static ref List<Pickup> PickupsRef(FetchManager manager) => ref manager.pickups;
    public static bool IsFetchablePickup(Pickupable pickup, FetchChore chore, Storage destination) => pickup.Allowed;
}
public sealed class FetchAreaChore
{
    public sealed class StatesInstance
    {
        public void Begin(Chore.Precondition.Context context) { }
    }
    public static void GatherNearbyFetchChores() { }
}
public static class Util
{
    public enum IterationInstruction { Continue, Halt }
}

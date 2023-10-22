using System;
using System.Collections.Generic;
using System.Linq;
using JasperFx.Core;
using Marten.Events.CodeGeneration;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace EventSourcingTests.Projections.CodeGeneration;


public class EventTypePatternMatchFrameOrderTests
{
    private readonly ITestOutputHelper _outputHelper;

    public EventTypePatternMatchFrameOrderTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [MemberData(nameof(GetEventTypeCombinations))]
    public void SortByEventTypeHierarchy_WithCombinations_SortsAsExpected(TypeArrayData events) =>
        RunSortByHierarchyTest(events);

    public static IEnumerable<object[]> GetEventTypeCombinations() =>
        GetCombinationsEventsData(3, 6,
            typeof(Base), typeof(IFoo), typeof(IBar),
            typeof(FooBase), typeof(FooA), typeof(FooX),
            typeof(BarBase), typeof(BarA), typeof(BarX),
            typeof(FooBarA), typeof(FooBarX) 
        );


    [Theory]
    [MemberData(nameof(GetEventTypePermutations))]
    public void SortByEventTypeHierarchy_WithPermutations_SortsAsExpected(TypeArrayData events) =>
        RunSortByHierarchyTest(events);

    public static IEnumerable<object[]> GetEventTypePermutations() =>
        GetPermutationsEventsData(typeof(FooBase), typeof(FooA), typeof(FooX), typeof(BarBase), typeof(FooBarA), typeof(FooBarX));

    private void RunSortByHierarchyTest(TypeArrayData events)
    {
        var frames = events.Data.ToDummyEventProcessingFrames();
        var sortedFrames = EventTypePatternMatchFrame.SortByEventTypeHierarchy(frames).ToArray();

        var eventTypes = sortedFrames.Select(p => p.EventType).ToArray();
        _outputHelper.WriteLine($"{events} => {new TypeArrayData(eventTypes)}");

        sortedFrames.Length.ShouldBe(frames.ToArray().Length);
        sortedFrames.ShouldBe(frames, ignoreOrder: true);
        eventTypes.ShouldHaveDerivedTypesBeforeBaseTypes();
    }

    private static IEnumerable<object[]> GetPermutationsEventsData(params Type[] events) =>
        events.GetPermutations()
            .Select(p => new object[] { new TypeArrayData(p.ToArray()) });

    private static IEnumerable<object[]> GetCombinationsEventsData(int minSize, int maxSize, params Type[] events) =>
        events.GetCombinations()
            .Where(p => minSize <= p.Count() && p.Count() <= maxSize)
            .Select(p => new object[] { new TypeArrayData(p.ToArray()) });

    public class TypeArrayData
    {
        public Type[] Data { get; }

        public TypeArrayData(Type[] data)
        {
            Data = data;
        }

        public override string ToString()
        {
            return $"[{string.Join(", ", Data.Select(x => x.Name))}]";
        }
    }

    public class Base{}

    public interface IFoo {}
    public interface IBar { }

    public class FooBase : Base, IFoo { }
    public class BarBase: Base, IBar { }

    public class FooA : FooBase { }
    public class FooX: FooBase { }

    public class BarA: BarBase { }
    public class BarX: BarBase { }

    public class FooBarA: BarBase, IFoo { }
    public class FooBarX: BarBase, IFoo { }
}

internal static class EventTypePatternMatchFrameOrderTestsExtensions
{
    internal static void ShouldHaveDerivedTypesBeforeBaseTypes(this Type[] types)
    {
        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i];
            var index = types.IndexOf(type.BaseType);
            if (index != -1)
                index.ShouldBeGreaterThan(i, $"'{type.Name}' should come before '{type.BaseType?.Name}'.");
        }
    }

    internal static List<EventProcessingFrame> ToDummyEventProcessingFrames(this IEnumerable<Type> types) =>
        types
            .Select(p => new EventProcessingFrame(false, typeof(object), p))
            .ToList();

    internal static IEnumerable<IEnumerable<T>> GetCombinations<T>(this IEnumerable<T> elements)
    {
        elements = elements.ToArray();

        if (!elements.Any())
        {
            yield return Enumerable.Empty<T>();
        }
        else
        {
            var head = elements.Take(1);
            var tail = elements.Skip(1);

            foreach (var combination in GetCombinations(tail))
            {
                yield return combination;
                yield return head.Concat(combination);
            }
        }
    }

    internal static IEnumerable<T[]> GetPermutations<T>(this IEnumerable<T> elements)
    {
        var list = elements.ToArray();
        var n = list.Length;
        var indexes = new int[n];
        for (var i = 0; i < n; i++)
        {
            indexes[i] = 0;
        }

        yield return list;

        var j = 0;
        while (j < n)
        {
            if (indexes[j] < j)
            {
                var swapIndex = j % 2 == 0 ? 0 : indexes[j];
                (list[swapIndex], list[j]) = (list[j], list[swapIndex]);

                yield return list.Copy();

                indexes[j]++;
                j = 0;
            }
            else
            {
                indexes[j] = 0;
                j++;
            }
        }
    }
}

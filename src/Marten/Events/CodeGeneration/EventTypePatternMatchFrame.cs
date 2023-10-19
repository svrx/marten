using System;
using System.Collections.Generic;
using System.Linq;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using JasperFx.Core.Reflection;

namespace Marten.Events.CodeGeneration;

internal class EventTypePatternMatchFrame: Frame
{
    private readonly List<EventProcessingFrame> _inner;
    private Variable _event;

    public EventTypePatternMatchFrame(List<EventProcessingFrame> frames): base(frames.Any(x => x.IsAsync))
    {
        _inner = frames;
    }

    public override void GenerateCode(GeneratedMethod method, ISourceWriter writer)
    {
        if (_inner.Any())
        {
            writer.Write($"BLOCK:switch ({_event.Usage})");
            var sortedEventFrames = SortByEventTypeHierarchy(_inner).ToArray();
            if (sortedEventFrames.Length != _inner.Count)
            {
                throw new InvalidOperationException("Event types were lost during the sorting");
            }

            foreach (var frame in sortedEventFrames)
            {
                frame.GenerateCode(method, writer);
            }

            writer.FinishBlock();
        }

        Next?.GenerateCode(method, writer);
    }

    public override IEnumerable<Variable> FindVariables(IMethodVariables chain)
    {
        _event = chain.FindVariable(typeof(IEvent));

        yield return _event;

        foreach (var variable in _inner.SelectMany(x => x.FindVariables(chain))) yield return variable;
    }

    /// <summary>
    /// Sort event processing frames by event type hierarchy
    /// </summary>
    /// <param name="frames"></param>
    /// <returns></returns>
    internal static IEnumerable<EventProcessingFrame> SortByEventTypeHierarchy(IEnumerable<EventProcessingFrame> frames)
    {
        return frames.OrderBy(frame => frame, new EventTypeComparer());
    }

    /// <summary>
    /// Sort frames by event type hierarchy
    /// <remarks>Comparer is not safe to use outside of intended purpose</remarks>
    /// </summary>
    private class EventTypeComparer: IComparer<EventProcessingFrame>
    {
        public int Compare(EventProcessingFrame x, EventProcessingFrame y)
        {
            using var xh = GetTypeHierarchy(x?.EventType).GetEnumerator();
            using var yh = GetTypeHierarchy(y?.EventType).GetEnumerator();

            while (true)
            {
                var current = (
                    x: xh.MoveNext() ? xh.Current : null,
                    y: yh.MoveNext() ? yh.Current : null
                );

                switch (current)
                {
                    case (null, null):
                        return 0; //Not expected to get here
                    case (null, _):
                        return 1; //Y is more derived, Y>X 
                    case (_, null):
                        return -1; //X is more derived, Y<X
                    case (_, _):
                        var comparison = StringComparer.OrdinalIgnoreCase.Compare(current.x.Name, current.y.Name);
                        if (comparison != 0)
                            return comparison;
                        break;
                }
            }
        }

        private static IEnumerable<Type> GetTypeHierarchy(Type type)
        {
            var hierarchy = new List<Type>(5);
            while (type != null)
            {
                hierarchy.Add(type);
                type = type.BaseType;
            }

            return hierarchy
                .AsEnumerable()
                .Reverse();
        }
    }
}

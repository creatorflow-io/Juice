using System.Text;

namespace Juice.Workflows.Helpers
{
    public class ContextPrintHelper
    {
        public static string Visualize(WorkflowContext context)
            => new ContextPrintHelper().GetVisualize(context);

        public string GetVisualize(WorkflowContext context)
        {
            var start = context.GetStartNode(default);

            Row(0).Insert(20, "--- funny workflow visualization! ---");
            PrintBranches(context, start, 1, 0);

            var _string = new StringBuilder();
            while (_rows.TryDequeue(out var row))
            {
                _string.AppendLine(row.ToString().TrimEnd());
            }
            return _string.ToString();

        }
        private StringBuilder Row(int row)
        {
            while (_rows.Count <= row)
            {
                _rows.Enqueue(new StringBuilder(new string(' ', 500)));
            }
            return _rows.ElementAt(row);
        }
        private int PrintBranches(WorkflowContext context, NodeContext node, int row, int col)
        {
            var topRow = Row(row);
            var midRow = Row(row + 1);
            var btRow = Row(row + 2);
            var padWidth = 0;
            var currentPoint = col;
            if (node.Node is IGateway)
            {
                PrintGateway(node, midRow, col);
                padWidth = 2;
                currentPoint += 3;
            }
            else if (node.Node is IEvent)
            {
                PrintEvent(node, midRow, col);
                padWidth = 2;
                currentPoint += 3;
            }
            else if (node.Node is SubProcess)
            {
                currentPoint = PrintSubProcess(context, node, row + 1, col);
            }
            else
            {
                topRow.Replace(' ', '-', col, _nodeWidth);
                PrintActivity(node, midRow, col);
                btRow.Replace(' ', '-', col, _nodeWidth);

                padWidth = _nodeWidth / 2 + 1;
                currentPoint += _nodeWidth;
            }

            var i = 0;

            var nodeCenterPoint = currentPoint - padWidth;

            _printedNodes.Add(node.Record.Id, new Location(row + 1, nodeCenterPoint));

            var currentRow = row;

            var rightBoundaryCol = currentPoint;
            foreach (var flow in context.GetOutgoings(node))
            {
                var next = context.GetNode(flow.Record.DestinationRef);

                if (_printedNodes.ContainsKey(flow.Record.DestinationRef))
                {
                    // Directly flow from gateway to gateway
                    if (next.Node is IGateway && context.GetNode(flow.Record.SourceRef).Node is IGateway)
                    {
                        currentRow += 3;
                        Vertical(Row(currentRow - 1), nodeCenterPoint);
                        var endFlow = Fork(context, flow, currentRow, currentPoint, padWidth);
                        rightBoundaryCol = Math.Max(rightBoundaryCol, Merge(context, flow, row, endFlow, padWidth));
                    }
                    else
                    {
                        rightBoundaryCol = Math.Max(rightBoundaryCol, Merge(context, flow, row, currentPoint, padWidth));
                    }
                }
                else
                {
                    if (i == 0)
                    {
                        var endFlow = PrintFlow(context, flow, midRow, currentPoint);

                        if (next != null)
                        {
                            rightBoundaryCol = Math.Max(rightBoundaryCol, PrintBranches(context, next, row, endFlow));
                        }
                        else
                        {
                            midRow.Replace("    ", "NULL", endFlow, 4);
                        }
                    }
                    else
                    {
                        currentRow += 3;
                        Vertical(Row(currentRow - 1), nodeCenterPoint);
                        var endFlow = Fork(context, flow, currentRow, currentPoint, padWidth);

                        if (next != null)
                        {
                            rightBoundaryCol = Math.Max(rightBoundaryCol, PrintBranches(context, next, currentRow, endFlow));
                        }
                        else
                        {
                            midRow.Replace("     ", ">NULL", currentPoint, 5);
                        }
                    }
                }
                i++;
            }
            return rightBoundaryCol;
        }
        private int Merge(WorkflowContext context, FlowContext flow, int row, int currentPoint, int padWidth)
        {
            var topRow = Row(row);
            var midRow = Row(row + 1);
            var btRow = Row(row + 2);
            var centerPoint = currentPoint - padWidth;

            var mergeLocation = _printedNodes[flow.Record.DestinationRef];

            var mergePoint = mergeLocation.Point;

            var mergeType = context.GetNode(flow.Record.DestinationRef).Node;

            var startRow = mergeType is IGateway || mergeType is IEvent
                ? mergeLocation.Row + 1 : mergeLocation.Row + 2;
            Row(startRow).Replace(' ', '^', mergePoint, 1);

            var text = context.FlowSnapshots.Any(s => s.Id == flow.Record.Id)
                    ? context.FlowSnapshots.IndexOf(context.FlowSnapshots.First(s => s.Id == flow.Record.Id)).ToString()
                    : flow.DisplayName;

            if (mergePoint >= currentPoint)
            {
                for (var i = startRow + 1; i <= row; i++)
                {
                    var betweenRow = Row(i);
                    betweenRow.Replace(' ', '|', mergePoint, 1);
                }

                Horizontal(midRow, currentPoint, mergePoint, text);

                midRow.Replace(' ', '\'', mergePoint, 1);
                return mergePoint;
            }
            else
            {
                for (var i = startRow + 1; i < row - 2; i++)
                {
                    var betweenRow = Row(i);
                    Vertical(betweenRow, mergePoint);
                }
                HalfVertical(Row(row - 1), mergePoint);

                Horizontal(Row(row - 1), centerPoint, mergePoint, text);
                HalfVertical(topRow, centerPoint);

                return currentPoint;
            }
        }
        private void Vertical(StringBuilder row, int point)
        {
            row.Replace(' ', '|', point, 1).Replace('-', '|', point, 1);
        }
        private void HalfVertical(StringBuilder row, int point)
        {
            row.Replace(' ', '\'', point, 1).Replace('-', '\'', point, 1);
        }
        private void Horizontal(StringBuilder row, int from, int to, string? condition = default)
        {
            condition = condition ?? "";
            if (condition.Length > 8)
            {
                condition = condition.Substring(0, 8);
            }

            if (from > to)
            {
                var tmp = from; from = to; to = tmp;
            }

            if (condition.Length > 0 && condition.Length < to - from)
            {
                var preLen = (to - from - condition.Length) / 2;
                row.Replace(' ', '-', from, to - from);
                row.Replace(new string('-', condition.Length), condition, from + preLen, condition.Length);
            }
            else
            {
                row.Replace(' ', '-', from, to - from);
            }
        }
        private int Fork(WorkflowContext context, FlowContext flow, int row, int currentPoint, int padWidth)
        {
            var centerPoint = currentPoint - padWidth;
            var topRow = Row(row);
            var midRow = Row(row + 1);

            topRow.Replace(' ', '|', centerPoint, 1);
            midRow.Replace(' ', '\'', currentPoint - 2, 1)
                .Replace(' ', '-', currentPoint - 1, 1);

            if (context.GetNode(flow.Record.SourceRef).Node is IGateway
                && context.GetNode(flow.Record.DestinationRef).Node is IGateway)
            {
                Merge(context, flow, row, currentPoint, padWidth);
                return _printedNodes[flow.Record.DestinationRef].Point;
            }
            return PrintFlow(context, flow, midRow, currentPoint);
        }
        private void PrintEvent(NodeContext node, StringBuilder builder, int start)
        {
            if (node.Node is StartEvent)
            {
                builder.Replace("   ", "( )", start, 3);
            }
            else if (node.Node is EndEvent)
            {
                builder.Replace("   ", "())", start, 3);
            }
            else if (node.Node is IIntermediate)
            {
                builder.Replace("   ", "(O)", start, 3);
            }
        }
        private void PrintGateway(NodeContext node, StringBuilder builder, int start)
        {
            if (node.Node is ExclusiveGateway)
            {
                builder.Replace("   ", "<X>", start, 3);
            }
            else if (node.Node is ParallelGateway)
            {
                builder.Replace("   ", "<+>", start, 3);
            }
            else if (node.Node is InclusiveGateway)
            {
                builder.Replace("   ", "<O>", start, 3);
            }
            else if (node.Node is EventBasedGateway)
            {
                builder.Replace("   ", "<~>", start, 3);
            }
        }
        private void PrintActivity(NodeContext node, StringBuilder builder, int start)
        {
            Vertical(builder, start);

            builder.Replace(new string(' ', node.DisplayName.Length), node.DisplayName, start + (_nodeWidth - node.DisplayName.Length) / 2 + 1, node.DisplayName.Length);

            Vertical(builder, start + _nodeWidth);
        }
        private int PrintSubProcess(WorkflowContext context, NodeContext node, int row, int start)
        {
            Vertical(Row(row - 1), start);
            Vertical(Row(row), start);
            Vertical(Row(row + 1), start);

            Row(row + 1).Replace(' ', '_', start, 2).Replace('-', '_', start, 2);

            Row(row - 1).Replace(new string(' ', node.DisplayName.Length), node.DisplayName, start + 2, node.DisplayName.Length);

            var startNode = context.GetStartNode(node.Record.Id);

            var rightBoundaryCol = PrintBranches(context, startNode, row - 1, start + 4);

            Vertical(Row(row - 1), rightBoundaryCol + 1);
            Vertical(Row(row), rightBoundaryCol + 1);
            Vertical(Row(row + 1), rightBoundaryCol + 1);
            Row(row + 1).Replace(' ', '_', rightBoundaryCol - 2, 2).Replace('-', '_', rightBoundaryCol - 2, 2);

            return rightBoundaryCol + 1;
        }
        private int PrintFlow(WorkflowContext context, FlowContext flow, StringBuilder builder, int start)
        {
            var text = context.FlowSnapshots.Any(s => s.Id == flow.Record.Id)
                    ? context.FlowSnapshots.IndexOf(context.FlowSnapshots.First(s => s.Id == flow.Record.Id)).ToString()
                    : flow.DisplayName;
            if (context.IsDefaultOutgoing(flow, default))
            {
                builder.Replace(' ', '/', start, 1);
                Horizontal(builder, start + 1, start + _flowLength - 1,
              text);
            }
            else
            {
                Horizontal(builder, start, start + _flowLength - 1,
                  text);
            }
            builder.Replace(' ', '>', start + _flowLength - 1, 1);
            return start + _flowLength;
        }
        private string AlignCentre(string text, int width)
        {
            text = text.Length > width ? text.Substring(0, width - 3) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                return new string(' ', width);
            }
            else
            {
                return text.PadRight(width - (width - text.Length) / 2).PadLeft(width);
            }
        }
        private readonly int _nodeWidth = 15;
        private readonly int _flowLength = 10;
        private Queue<StringBuilder> _rows = new Queue<StringBuilder>();
        private Dictionary<string, Location> _printedNodes = new Dictionary<string, Location>();
        private record Location(int Row, int Point);
    }
}

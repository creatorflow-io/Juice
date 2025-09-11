using System.Text;

namespace Juice.Utils
{
    public enum ColAlign { left, center, right }
    public class ConsoleTable
    {
        private int _tableWidth = 12 * 9;
        private int[]? _cols;
        private ColAlign[]? _colsAlign;
        private ColAlign[]? _headsAlign;
        private string[][] _headers;
        private string[][] _rows;
        private StringBuilder _sb = new ();

        public ConsoleTable(string[][] aheaders, string[][] arows)
        {
            this._headers = aheaders;
            this._rows = arows;
        }

        public int Width
        {
            get { return _tableWidth; }
            set { _tableWidth = value; }
        }

        public int[] Cols
        {
            set
            {
                _cols = value;
                int w = 0;
                foreach (int col in _cols)
                {
                    w += col + 1;
                }
                if (w > 0) { Width = w; }
            }
        }

        public ColAlign[] ColsAlign
        {
            set
            {
                _colsAlign = value;
            }
        }

        public ColAlign[] HeadsAlign
        {
            set
            {
                _headsAlign = value;
            }
        }

        public string PrintTable()
        {
            _sb.Clear();
            if ((_headers != null && _headers.Length > 0) || (_rows != null && _rows.Length > 0))
            {
                PrintTopLine();
            }
            if (_headers != null && _headers.Length > 0)
            {
                for (var i = 0; i < _headers.Length; i++)
                {
                    PrintRow(_headers[i], _headsAlign);
                    PrintLine();
                }
            }

            if (_rows != null)
            {
                for (var i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i].Length == 0)
                    {
                        if(i > 0) { PrintLine(); }
                    }
                    else
                    {
                        PrintRow(_rows[i], _colsAlign);
                    }
                }
                PrintBottomLine();
            }
            return _sb.ToString();
        }

        private void AlignCentre(string text, int width)
        {
            text = text.Length > width ? text.Substring(0, width - 3) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                _sb.Append(new string(' ', width));
            }
            else
            {
                _sb.Append(text.PadRight(width - (width - text.Length) / 2).PadLeft(width));
            }
        }

        private void AlignLeft(string text, int width, int align = 1)
        {
            text = text.Length > width ? text.Substring(0, width - 3 - align) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                _sb.Append(new string(' ', width));
            }
            else
            {
                _sb.Append(text.PadRight(width - align).PadLeft(width));
            }
        }

        private void AlignRight(string text, int width, int align = 1)
        {
            text = text.Length > width ? text.Substring(0, width - 3 - align) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                _sb.Append(new string(' ', width));
            }
            else
            {
                _sb.Append(text.PadLeft(width - align).PadRight(width));
            }
        }

        private void PrintRow(string[] columns, ColAlign[]? colsAlign)
        {
            _sb.Append("|");
            if (_cols == null || columns.Length < _cols.Length)
            {
                var width = (_tableWidth - columns.Length) / columns.Length;
                for (var i = 0; i < columns.Length; i++)
                {
                    var column = columns[i];
                    if (colsAlign != null && colsAlign.Length > i)
                    {
                        switch (colsAlign[i])
                        {
                            case ColAlign.left:
                                AlignLeft(column, width);
                                break;
                            case ColAlign.right:
                                AlignRight(column, width);
                                break;
                            default:
                                AlignCentre(column, width);
                                break;
                        }
                    }
                    else
                    {
                        AlignCentre(column, width);
                    }
                    _sb.Append("|");
                }
            }
            else
            {
                for (var i = 0; i < _cols.Length; i++)
                {
                    var colValue = columns.Length > i ? columns[i] : "";
                    if (colsAlign != null && colsAlign.Length > i)
                    {
                        switch (colsAlign[i])
                        {
                            case ColAlign.left:
                                AlignLeft(colValue, _cols[i]);
                                break;
                            case ColAlign.right:
                                AlignRight(colValue, _cols[i]);
                                break;
                            default:
                                AlignCentre(colValue, _cols[i]);
                                break;
                        }
                    }
                    else
                    {
                        AlignCentre(colValue, _cols[i]);
                    }
                    _sb.Append("|");
                }
            }
            _sb.AppendLine();
        }

        private void PrintTopLine()
        {
            _sb.AppendLine(" " + new string('_', _tableWidth - 1));
        }

        private void PrintBottomLine()
        {
            _sb.AppendLine("'" + new string('-', _tableWidth - 1) + "'");
        }

        private void PrintLine()
        {
            _sb.AppendLine("|" + new string('-', _tableWidth - 1) + "|");
        }

        private void PrintLine(string s)
        {
            _sb.AppendLine();
            _sb.Append("|");
            AlignCentre(s, _tableWidth);
            _sb.Append("|");
        }
    }
}

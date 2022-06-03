using jar;
using System;
using System.Text;

class Init {
    private static int Main(string[] args) {
        string line;
        var inputBuilder = new StringBuilder();
        while ((line = Console.ReadLine()) != null) {
            inputBuilder.Append(line);
            inputBuilder.Append('\n');
        }

        var src = inputBuilder.ToString();

        if (args.Length < 1) return -1;

        var l = new Loc[1 << 10];
        var len = Jar.Parse(src, l, 0);
        if (l[0].tok != Tok.Obj) return -1;
        int j = 1;
        var o = new StringBuilder();

        var b = new StringBuilder[100];

        int depth = 0;
        for (int i = 1; i < args.Length; i++) {
            o.Append("using ");
            o.Append(args[i]);
            o.Append(";\n");
        }
        o.Append("using jar;\n");
        o.Append("using rin;\n\n");
        o.Append("namespace ");
        o.Append(args[0]);
        o.Append(" {\n");

        var start = 3;
        while (j < l[0].width) {
            var cls = l[j++];
            var dsc = l[j++];

            if (cls.len == 8 && string.Compare(src, cls.start, "__slaves", 0, 8) == 0) {
                if (j == 3) start = j + dsc.width + 2;
                j += dsc.width;
                continue;
            }

            if (j != start) o.Append("\n");

            Indent(o, ++depth);
            o.Append("public static class ");
            o.Append(src, cls.start, cls.len);
            o.Append("JAR");
            o.Append(" {\n");

            Indent(o, ++depth);
            o.Append("public static ");
            o.Append(src, cls.start, cls.len);
            o.Append(" Parse(string t, Loc[] l, int start, Rin rin) {\n");

            Indent(o, ++depth);
            o.Append("var obj = new ");
            o.Append(src, cls.start, cls.len);
            o.Append("();\n");
            Indent(o, depth);
            o.Append("if (t == null || l == null || start < 0 || start >= l.Length) ");
            o.Append("return obj;\n");
            Indent(o, depth);
            o.Append("var end = start + l[start].width;\n");
            Indent(o, depth);
            o.Append("if (l.Length < end) return obj;\n\n");

            Indent(o, depth);
            o.Append("int j = start + 1;\n");
            Indent(o, depth);
            o.Append("while (j < end) {\n");
            Indent(o, ++depth);
            o.Append("var key = l[j++];\n");
            Indent(o, depth);
            o.Append("if (j > end) return obj;\n");
            Indent(o, depth);
            o.Append("switch (key.len) {\n");
            depth++;

            var i = j;
            while (i < j + dsc.width) {
                var key = l[i++];
                var val = l[i++];

                if (key.len == 8 && string.Compare(src, key.start, "__is_ref", 0, 8) == 0) {
                    i += val.width;
                    continue;
                }

                var isNull = val.len > 0 && src[val.start + val.len - 1] == '?';
                if (isNull) val.len--;

                if (b[key.len] == null) b[key.len] = new StringBuilder();
                var bo = b[key.len];
                var bd = depth;

                var first = bo.Length == 0;
                if (first) {
                    Indent(bo, bd);
                    bo.Append("case ");
                    bo.Append(key.len);
                    bo.Append(":\n");
                }

                ++bd;
                if (first) {
                    Indent(bo, bd);
                } else bo.Append(" else ");

                bo.Append("if (string.Compare(t, key.start, \"");
                bo.Append(src, key.start, key.len);
                bo.Append("\", 0, ");
                bo.Append(key.len);
                bo.Append(") == 0) {\n");

                bd++;
                bd = GenParse(src, bo, src.Substring(key.start, key.len), true, val, bd);

                Indent(bo, bd--);
                bo.Append("}");

                i += val.width;
            }

            depth++;
            for (int k = 0; k < b.Length; k++) if (b[k] != null && b[k].Length > 0) {
                o.Append(b[k]);
                o.Append(" else j++;\n");
                Indent(o, depth);
                o.Append("break;");
                if (k != b.Length - 1) o.Append('\n');
                b[k].Clear();
            }

            Indent(o, depth - 1);
            o.Append("default:\n");
            Indent(o, depth);
            o.Append("j++;\n");
            Indent(o, depth);
            o.Append("break;\n");
            depth--;

            Indent(o, --depth);
            o.Append("}\n");
            Indent(o, --depth);
            o.Append("}\n");

            Indent(o, depth);
            o.Append("return obj;\n");

            Indent(o, --depth);
            o.Append("}\n");

            Indent(o, --depth);
            o.Append("}\n");
            depth--;

            j += dsc.width;
        }

        Indent(o, --depth);
        o.Append("}");

        Console.WriteLine(o.ToString());
        return 0;
    }

    private static int GenParse(string src, StringBuilder o, string key, bool obj, Loc v, int d) {
        var isStr = v.len == 6 && string.Compare(src, v.start, "string", 0, 6) == 0;
        var num = WhatNum(src, v.start, v.len);
        var isBool = v.len == 4 && string.Compare(src, v.start, "bool", 0, 4) == 0;
        var lastTwo = v.start + v.len - 2;
        var isArr = v.len > 2 && string.Compare(src, lastTwo, "[]", 0, 2) == 0;
        var isList = v.len > 5 && string.Compare(src, v.start, "List<", 0, 5) == 0;
        var isDict = v.len > 19 && string.Compare(src, v.start, "Dictionary<string, ", 0, 19) == 0;
        var isEnum = v.len > 10 && string.Compare(src, v.start, "enum ", 0, 5) == 0;

        if (isEnum) {
            v.start += 5;
            v.len -= 5;
        }

        Indent(o, d);
        if (isStr) {
            o.Append("if (l[j].tok == Tok.Str) {\n");
            Indent(o, ++d);
            if (obj) o.Append("obj.");
            o.Append(key);
            o.Append(" = rin.Un(t.Substring(l[j].start, l[j].len));\n");
            Indent(o, --d);
            o.Append("} else ");
            if (obj) o.Append("obj.");
            o.Append(key);
            o.Append(" = null;\n");
            Indent(o, d--);
            o.Append("j++;\n");
        } else if (num != null) {
            o.Append("var ");
            o.Append(key);
            o.Append(" = Rin.");
            o.Append(num);
            o.Append("(t.Substring(l[j].start, l[j].len));\n");
            Indent(o, d);
            o.Append("if (");
            o.Append(key);
            o.Append(" != null) ");
            if (obj) o.Append("obj.");
            o.Append(key);
            o.Append(" = ");
            o.Append(key);
            o.Append(".Value;\n");
            Indent(o, d--);
            o.Append("j++;\n");
        } else if (isBool) {
            if (obj) o.Append("obj.");
            o.Append(key);
            o.Append(" = l[j].len == 4;\n");
            Indent(o, d--);
            o.Append("j++;\n");
        } else if (isArr || isList) {
            if (obj) o.Append("obj.");
            o.Append(key);
            o.Append(" = new ");
            if (isArr) o.Append(src, v.start, v.len - 2); else o.Append(src, v.start, v.len);
            if (isArr) o.Append("[l[j].size];\n"); else o.Append("(l[j].size);\n");
            Indent(o, d);
            o.Append("var size = l[j].size;\n");
            Indent(o, d);
            o.Append("j++;\n");
            Indent(o, d);
            o.Append("for (int k = 0; k < size && j < l.Length; k++) {\n");
            d++;
            if (isArr) v.len -= 2; else {
                v.start += 5;
                v.len -= 6;
            }
            var arrKey = key + "[k]";
            if (isList) {
                Indent(o, d);
                o.Append(src, v.start, v.len);
                o.Append(" item;\n");
            }
            if (isList) arrKey = "item";
            GenParse(src, o, arrKey, isArr, v, d);
            if (isList) {
                Indent(o, d);
                if (obj) o.Append("obj.");
                o.Append(key);
                o.Append(".Add(item);\n");
            }
            Indent(o, --d);
            o.Append("}\n");
            d--;
        } else if (isDict) {
            if (obj) o.Append("obj.");
            o.Append(key);
            o.Append(" = new Dictionary<string, ");
            o.Append(src, v.start + 19, v.len - 20);
            o.Append(">();\n");
            Indent(o, d);
            o.Append("var size = l[j].size;\n");
            Indent(o, d);
            o.Append("j++;\n");
            Indent(o, d);
            o.Append("for (int k = 0; k < size / 2 && j < l.Length; k++) {\n");
            Indent(o, ++d);
            o.Append("var dk = rin.Un(t.Substring(l[j].start, l[j].len));\n");
            Indent(o, d);
            o.Append("if (++j > end) return obj;\n");
            v.start += 19;
            v.len -= 20;
            GenParse(src, o, $"{key}[dk]", true, v, d);
            Indent(o, --d);
            o.Append("}\n");
            d--;
        } else if (isEnum) {
            o.Append("var ");
            o.Append(key);
            o.Append(" = ");
            o.Append("Rin.Int(t.Substring(l[j].start, l[j].len));\n");
            Indent(o, d);
            o.Append("if (");
            o.Append(key);
            o.Append(" != null) ");
            if (obj) o.Append("obj.");
            o.Append(key);
            o.Append(" = (");
            o.Append(src, v.start, v.len);
            o.Append(")");
            o.Append(key);
            o.Append(".Value;\n");
            Indent(o, d);
            o.Append("j++;\n");
            d--;
        } else {
            if (obj) o.Append("obj.");
            o.Append(key);
            o.Append(" = ");
            o.Append(src, v.start, v.len);
            o.Append("JAR.Parse(t, l, j, rin);\n");
            Indent(o, d--);
            o.Append("j += l[j].width + 1;\n");
        }

        return d;
    }

    private static string WhatNum(string src, int start, int len) {
        switch (len) {
            case 3:
                if (string.Compare(src, start, "int", 0, 3) == 0) return "Int";
                break;
            case 4:
                if (string.Compare(src, start, "byte", 0, 4) == 0) {
                    return "Byte";
                } else if (string.Compare(src, start, "uint", 0, 4) == 0) {
                    return "Uint";
                } else if (string.Compare(src, start, "long", 0, 4) == 0) {
                    return "Long";
                }
                break;
            case 5:
                if (string.Compare(src, start, "sbyte", 0, 5) == 0) {
                    return "Sbyte";
                } else if (string.Compare(src, start, "short", 0, 5) == 0) {
                    return "Short";
                } else if (string.Compare(src, start, "ulong", 0, 5) == 0) {
                    return "Ulong";
                } else if (string.Compare(src, start, "float", 0, 5) == 0) {
                    return "Float";
                }
                break;
            case 6:
                if (string.Compare(src, start, "ushort", 0, 6) == 0) {
                    return "Ushort";
                } else if (string.Compare(src, start, "double", 0, 6) == 0) {
                    return "Double";
                }
                break;
            case 7:
                if (string.Compare(src, start, "decimal", 0, 7) == 0) return "Decimal";
                break;
        }

        return null;
    }

    private static void Indent(StringBuilder o, int depth) {
        for (int i = 0; i < depth; i++) o.Append("    ");
    }
}

using System.CommandLine;
using System.Text;
using System.Text.RegularExpressions;

Argument<FileSystemInfo> inputArgument = new(
  name: "input",
  description: "The input file or directory",
  getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory())
);

RootCommand rootCommand = new("An indentation-based Lisp syntax transpiler") {
  inputArgument,
};
rootCommand.SetHandler(App, inputArgument);
await rootCommand.InvokeAsync(args);

void App(FileSystemInfo input) {
  try {
    switch (input) {
      case FileInfo file:
        if (file.Extension == ".wlisp") CompileFile(file.FullName);
        else Console.WriteLine("Incorrect file extension");
        break;
      case DirectoryInfo dir:
        var files = dir.EnumerateFiles("*.wlisp", SearchOption.AllDirectories);
        foreach (var file in files) CompileFile(file.FullName);
        break;
      default:
        Console.WriteLine("File or directory not found");
        break;
    }
  } catch (Exception ex) {
    Console.WriteLine(ex.Message);
  }
}

void CompileFile(string path) {
  char? indentChar = null;
  var lineNumber = 0;
  string[] lines = File.ReadAllLines(path);

  File.WriteAllLines(Path.ChangeExtension(path, ".lisp"), GetChildren(-1));
  Console.WriteLine(path);

  string CompileSubtree(string line, int indent) {
    StringBuilder text = new();
    var tokenCount = CountTokens(line);
    List<string> children = GetChildren(indent);
    var joinedChildren = string.Join(Environment.NewLine, children);
    text.Append($"{line[..indent]}");
    if (children.Count + tokenCount > 1) text.Append('(');
    if (!MatchBlockHeader().IsMatch(line.Trim())) {
      text.Append($"{line[indent..]}");
      if (children.Count > 0) text.AppendLine();
      text.Append(joinedChildren);
    } else {
      text.Append(joinedChildren.TrimStart());
    }
    if (children.Count + tokenCount > 1) text.Append(')');
    return text.ToString();
  }

  List<string> GetChildren(int parentIndent) {
    List<string> children = [];
    int? subtreeIndent = null;
    while (NextLine() is string line) {
      if (line.All(char.IsWhiteSpace) || line.TrimStart()[0] == ';') {
        continue;
      }
      var lineIndent = CountLeadingWhiteSpace(line);
      if (lineIndent > parentIndent) {
        subtreeIndent ??= lineIndent;
        if (lineIndent != subtreeIndent) Error("Inconsistent indentation");
        children.Add(CompileSubtree(line, lineIndent));
      } else {
        if (lineIndent == parentIndent && line[parentIndent] == '|') {
          children.Add(
            line[..parentIndent] + indentChar + line[(parentIndent + 1)..]
          );
        } else {
          lineNumber--;
          break;
        }
      }
    }
    return children;
  }
  
  string? NextLine() => lineNumber < lines.Length ? lines[lineNumber++] : null;

  int CountLeadingWhiteSpace(string line) {
    var count = 0;
    foreach (var ch in line) {
      if (ch != ' ' && ch != '\t') break;
      indentChar ??= ch;
      if (indentChar != ch) Error("Mixed use of tabs and spaces");
      count++;
    }
    return count;
  }

  int CountTokens(string line) {
    line = MatchLispString().Replace(line, "s");
    var commentIndex = line.IndexOf(';');
    if (commentIndex >= 0) line = line[..commentIndex];
    StringBuilder parensFoldedLine = new();
    var parensDepth = 0;
    foreach (var ch in line) {
      switch (ch) {
        case '(':
          parensDepth++;
          break;
        case ')':
          parensDepth--;
          if (parensDepth == 0) parensFoldedLine.Append('p');
          else if (parensDepth < 0) Error("Mismatched parentheses");
          break;
        default:
          if (parensDepth == 0) parensFoldedLine.Append(ch);
          break;
      }
    }
    if (parensDepth > 0) Error("Mismatched parentheses");
    line = parensFoldedLine.ToString();
    return line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
  }

  void Error(string message) =>
    throw new Exception($"{Path.GetFileName(path)}:{lineNumber}: {message}");
}

partial class Program {
  [GeneratedRegex(@"""(\\\\|\\""|.)*?""")]
  private static partial Regex MatchLispString();
  [GeneratedRegex(@"^(\.+|-+|_+|‾+)$")]
  private static partial Regex MatchBlockHeader();
}

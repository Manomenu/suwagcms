namespace Suwagcms.Contracts;

/// <summary>One line of a compared text; a folded one says how many unchanged lines it stands for.</summary>
public sealed record TextLine(LineChange Change, string Text);

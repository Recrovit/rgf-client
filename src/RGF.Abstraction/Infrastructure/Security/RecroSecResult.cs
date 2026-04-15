namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;

public class RecroSecQuery
{
    public RecroSecQuery() { }

    public RecroSecQuery(RecroSecQuery query) : this(query.ObjectName, query.ObjectKey) { }

    public RecroSecQuery(string objectName, string objectKey = null)
    {
        ObjectName = objectName;
        ObjectKey = objectKey;
    }

    public string ObjectName { get; set; }

    public string ObjectKey { get; set; }

    public string EntityName { get; set; }
}

public class RecroSecResult
{
    public RecroSecResult() { }

    public RecroSecResult(RecroSecResult res) : this(res.Query, res.Permissions) { }

    public RecroSecResult(RecroSecQuery query, RgfPermissions perm)
    {
        Query = new(query);
        Permissions = new(perm);
    }

    public RecroSecQuery Query { get; set; }

    public RgfPermissions Permissions { get; set; }
}
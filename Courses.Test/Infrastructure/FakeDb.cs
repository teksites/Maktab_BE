using System.Collections;
using System.Data;
using System.Data.Common;
using Data;

namespace Courses.Test.Infrastructure;

internal sealed class FakeDatabase : IDatabase
{
    private readonly Func<DbDataReader> _readerFactory;
    private readonly Action<DbCommand>? _onExecute;

    public FakeDatabase(Func<DbDataReader> readerFactory, Action<DbCommand>? onExecute = null)
    {
        _readerFactory = readerFactory;
        _onExecute = onExecute;
    }

    public string ConnectionString => "Fake";

    public DbConnection CreateAndOpenConnection()
    {
        var connection = new FakeDbConnection(_readerFactory, _onExecute);
        connection.Open();
        return connection;
    }

    public Task<DbConnection> CreateAndOpenConnectionAsync()
        => Task.FromResult<DbConnection>(CreateAndOpenConnection());

    public DbCommand CreateCommand()
        => new FakeDbCommand(_readerFactory, _onExecute);

    public DbCommand CreateCommand(string cmdText)
    {
        var command = CreateCommand();
        command.CommandText = cmdText;
        return command;
    }

    public DbCommand CreateCommand(DbConnection conn)
    {
        var command = CreateCommand();
        command.Connection = conn;
        return command;
    }

    public DbCommand CreateCommand(string cmdText, DbConnection conn)
    {
        var command = CreateCommand(cmdText);
        command.Connection = conn;
        return command;
    }

    public DateTime? ConvertScalarToDateTime(object value, DateTime? deafaultIfInvalid = null)
    {
        if (value is DateTime dateTime)
        {
            return dateTime;
        }

        return deafaultIfInvalid;
    }
}

internal sealed class FakeDbConnection : DbConnection
{
    private readonly Func<DbDataReader> _readerFactory;
    private readonly Action<DbCommand>? _onExecute;
    private ConnectionState _state;

    public FakeDbConnection(Func<DbDataReader> readerFactory, Action<DbCommand>? onExecute)
    {
        _readerFactory = readerFactory;
        _onExecute = onExecute;
    }

    public override string? ConnectionString { get; set; } = "Fake";

    public override string Database => "Fake";

    public override string DataSource => "Fake";

    public override string ServerVersion => "1.0";

    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close()
    {
        _state = ConnectionState.Closed;
    }

    public override void Open()
    {
        _state = ConnectionState.Open;
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        Open();
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => throw new NotSupportedException();

    protected override DbCommand CreateDbCommand()
    {
        var command = new FakeDbCommand(_readerFactory, _onExecute)
        {
            Connection = this
        };

        return command;
    }
}

internal sealed class FakeDbCommand : DbCommand
{
    private readonly Func<DbDataReader> _readerFactory;
    private readonly Action<DbCommand>? _onExecute;
    private readonly FakeDbParameterCollection _parameters = new();

    public FakeDbCommand(Func<DbDataReader> readerFactory, Action<DbCommand>? onExecute)
    {
        _readerFactory = readerFactory;
        _onExecute = onExecute;
    }

    public override string? CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery() => 0;

    public override object? ExecuteScalar() => null;

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter()
        => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        _onExecute?.Invoke(this);
        return _readerFactory();
    }
}

internal sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    public override string? ParameterName { get; set; } = string.Empty;

    public override string? SourceColumn { get; set; } = string.Empty;

    public override object? Value { get; set; }

    public override bool SourceColumnNullMapping { get; set; }

    public override int Size { get; set; }

    public override void ResetDbType()
    {
    }
}

internal sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = new();

    public override int Count => _parameters.Count;

    public override object SyncRoot => ((ICollection)_parameters).SyncRoot!;

    public override int Add(object value)
    {
        _parameters.Add((DbParameter)value);
        return _parameters.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value!);
        }
    }

    public override void Clear()
    {
        _parameters.Clear();
    }

    public override bool Contains(string value)
        => _parameters.Any(parameter => parameter.ParameterName == value);

    public override bool Contains(object value)
        => _parameters.Contains((DbParameter)value);

    public override void CopyTo(Array array, int index)
        => ((ICollection)_parameters).CopyTo(array, index);

    public override IEnumerator GetEnumerator()
        => _parameters.GetEnumerator();

    protected override DbParameter GetParameter(string parameterName)
        => _parameters.Single(parameter => parameter.ParameterName == parameterName);

    protected override DbParameter GetParameter(int index)
        => _parameters[index];

    public override int IndexOf(string parameterName)
        => _parameters.FindIndex(parameter => parameter.ParameterName == parameterName);

    public override int IndexOf(object value)
        => _parameters.IndexOf((DbParameter)value);

    public override void Insert(int index, object value)
        => _parameters.Insert(index, (DbParameter)value);

    public override void Remove(object value)
        => _parameters.Remove((DbParameter)value);

    public override void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
        {
            _parameters.RemoveAt(index);
        }
    }

    public override void RemoveAt(int index)
        => _parameters.RemoveAt(index);

    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
        {
            _parameters[index] = value;
        }
    }

    protected override void SetParameter(int index, DbParameter value)
        => _parameters[index] = value;
}

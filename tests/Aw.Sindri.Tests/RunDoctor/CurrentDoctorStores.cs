using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Views;

namespace Aw.Sindri.Tests.RunDoctor;

/// <summary>The doctor's store reads for fakes with no files: the views store is at this build's migrations and every read runs as is.</summary>
internal sealed class CurrentDoctorStores : IDoctorStores
{
    public ValueTask<Result<SchemaStatus>> ViewsSchema(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SchemaStatus>>(new SchemaStatus(ViewsDatabase.Migrations.Count, ViewsDatabase.Migrations.Count));

    public ValueTask<Result<T>> Guard<T>(string file, Func<CancellationToken, ValueTask<Result<T>>> read, CancellationToken cancellationToken) => read(cancellationToken);
}

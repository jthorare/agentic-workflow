using Aw.Sindri.Host.Commands;

return await CommandTable.Run(args, Environment.GetEnvironmentVariable, TimeProvider.System).ConfigureAwait(false);

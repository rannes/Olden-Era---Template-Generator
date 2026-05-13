using System;
using System.Threading;
using System.Threading.Tasks;

namespace OldenEra.TemplateEditor.Services.AutoUpdate;

public interface IUpdateChecker
{
    Task<UpdateInfo?> CheckAsync(Version current, CancellationToken cancellationToken = default);
}

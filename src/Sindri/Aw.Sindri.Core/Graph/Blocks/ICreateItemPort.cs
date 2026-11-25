using Aw.Kernel;
namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'create_item' node's item is made: the same create-item handler 'sindri item add' and the app's New item form use.</summary>
public interface ICreateItemPort
{
    /// <summary>Creates the item.</summary>
    /// <param name="item">The item and the run that asks for it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new item and any warning, or why the item was not made.</returns>
    public ValueTask<Result<ItemMade>> Create(ItemToCreate item, CancellationToken cancellationToken);
}

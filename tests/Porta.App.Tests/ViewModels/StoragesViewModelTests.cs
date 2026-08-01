using Porta.App.Design;
using Porta.App.ViewModels;

namespace Porta.App.Tests.ViewModels;

public class StoragesViewModelTests
{
    [Fact]
    public void Starts_empty_when_repository_is_empty()
    {
        var vm = new StoragesViewModel(new InMemoryStorageRepository());
        Assert.Empty(vm.Items);
    }

    [Fact]
    public void Add_persists_storage_and_shows_it()
    {
        var repo = new InMemoryStorageRepository();
        var vm = new StoragesViewModel(repo)
        {
            NewName = "Docs",
            NewPath = "/home/me/docs",
        };

        vm.AddCommand.Execute(null);

        StorageItem item = Assert.Single(vm.Items);
        Assert.Equal("Docs", item.Name);
        Assert.Equal("/home/me/docs", item.LocalPath);
        Assert.Single(repo.List());
        Assert.Equal(string.Empty, vm.NewName); // поля очищены
        Assert.Equal(string.Empty, vm.NewPath);
    }

    [Theory]
    [InlineData("", "/path")]
    [InlineData("Name", "")]
    [InlineData("   ", "   ")]
    public void Add_ignores_blank_input(string name, string path)
    {
        var vm = new StoragesViewModel(new InMemoryStorageRepository())
        {
            NewName = name,
            NewPath = path,
        };

        vm.AddCommand.Execute(null);

        Assert.Empty(vm.Items);
    }
}

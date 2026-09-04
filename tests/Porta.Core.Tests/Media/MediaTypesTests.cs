using Porta.Core.Media;

namespace Porta.Core.Tests.Media;

public class MediaTypesTests
{
    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.JPEG")]
    [InlineData("/home/user/Pictures/img.heic")]
    [InlineData("raw.DNG")]
    public void Classifies_photos(string name)
        => Assert.Equal(MediaKind.Photo, MediaTypes.Classify(name));

    [Theory]
    [InlineData("clip.mp4")]
    [InlineData("clip.MOV")]
    [InlineData("/tmp/movies/film.mkv")]
    public void Classifies_videos(string name)
        => Assert.Equal(MediaKind.Video, MediaTypes.Classify(name));

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("archive.zip")]
    [InlineData("noextension")]
    [InlineData("")]
    public void Returns_null_for_non_media(string name)
        => Assert.Null(MediaTypes.Classify(name));
}

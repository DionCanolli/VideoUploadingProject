using System.Threading.Channels;

namespace VideoUploadingProject.Services;

public class LectureWorkQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void Enqueue(int lectureId) => _channel.Writer.TryWrite(lectureId);

    public ChannelReader<int> Reader => _channel.Reader;
}

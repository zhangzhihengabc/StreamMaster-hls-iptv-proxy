﻿using StreamMasterDomain.Pagination;

namespace StreamMasterApplication.StreamGroupVideoStreams.Queries;

public record GetPagedStreamGroupVideoStreams(StreamGroupVideoStreamParameters Parameters) : IRequest<PagedResponse<VideoStreamDto>>;

[LogExecutionTimeAspect]
internal class GetPagedStreamGroupVideoStreamsHandler(ILogger<GetPagedStreamGroupVideoStreams> logger, IRepositoryWrapper repository, IMapper mapper, ISettingsService settingsService) : BaseRequestHandler(logger, repository, mapper, settingsService), IRequestHandler<GetPagedStreamGroupVideoStreams, PagedResponse<VideoStreamDto>>
{
    public async Task<PagedResponse<VideoStreamDto>> Handle(GetPagedStreamGroupVideoStreams request, CancellationToken cancellationToken)
    {
        return await Repository.StreamGroupVideoStream.GetPagedStreamGroupVideoStreams(request.Parameters, cancellationToken).ConfigureAwait(false);
    }
}
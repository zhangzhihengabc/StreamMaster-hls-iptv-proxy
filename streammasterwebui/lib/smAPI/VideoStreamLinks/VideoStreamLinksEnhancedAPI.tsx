import { isEmptyObject } from '@lib/common/common';
import isPagedTableDto from '@lib/common/isPagedTableDto';
import type * as iptv from '@lib/iptvApi';
import { iptvApi } from '@lib/iptvApi';
import { isDev } from '@lib/settings';
import { singletonVideoStreamLinksListener } from '@lib/signalr/singletonListeners';

export const enhancedApiVideoStreamLinks = iptvApi.enhanceEndpoints({
  endpoints: {
    videoStreamLinksGetPagedVideoStreamVideoStreams: {
      async onCacheEntryAdded(api, { dispatch, getState, updateCachedData, cacheDataLoaded, cacheEntryRemoved }) {
        try {
          await cacheDataLoaded;

          const updateCachedDataWithResults = (data: iptv.VideoStreamDto[]) => {
            if (!data || isEmptyObject(data)) {
              if (isDev) console.log('VideoStreamLinks Full Refresh');
              dispatch(iptvApi.util.invalidateTags(['VideoStreamLinks']));
              return;
            }

            updateCachedData(() => {
              for (const { endpointName, originalArgs } of iptvApi.util.selectInvalidatedBy(getState(), [{ type: 'VideoStreamLinks' }])) {
                if (endpointName === 'videoStreamLinksGetPagedVideoStreamVideoStreams') {
                  dispatch(
                    iptvApi.util.updateQueryData(endpointName, originalArgs, (draft) => {
                      if (isPagedTableDto(data)) {
                        for (const item of data) {
                          const index = draft.data.findIndex((existingItem) => existingItem.id === item.id);
                          if (index !== -1) {
                            draft.data[index] = item;
                          }
                        }
                        return draft;
                      }
                      for (const item of data) {
                        const index = draft.data.findIndex((existingItem) => existingItem.id === item.id);
                        if (index !== -1) {
                          draft.data[index] = item;
                        }
                      }
                      return draft;
                    })
                  );
                }
              }
            });
          };

          singletonVideoStreamLinksListener.addListener(updateCachedDataWithResults);

          await cacheEntryRemoved;
          singletonVideoStreamLinksListener.removeListener(updateCachedDataWithResults);
        } catch (error) {
          console.error('Error in onCacheEntryAdded:', error);
        }
      }
      // eslint-disable-next-line comma-dangle
    }
  }
});

/* eslint unused-imports/no-unused-imports-ts: off */
/* eslint @typescript-eslint/no-unused-vars: off */
import { type StringArgument } from '@components/selectors/BaseSelector';
import type * as iptv from '@lib/iptvApi';
import { invokeHubConnection } from '@lib/signalr/signalr';

export const GetIcon = async (argument: iptv.IconFileDto): Promise<iptv.IconFileDto | null> => invokeHubConnection<iptv.IconFileDto>('GetIcon', argument);
export const GetIconFromSource = async (argument: StringArgument): Promise<iptv.IconFileDto | null> =>
  invokeHubConnection<iptv.IconFileDto>('GetIconFromSource', argument);
export const GetPagedIcons = async (argument: iptv.PagedResponseOfIconFileDto): Promise<iptv.IconFileDto[] | null> =>
  invokeHubConnection<iptv.IconFileDto[]>('GetPagedIcons', argument);
export const GetIconsSimpleQuery = async (argument: iptv.IconFileDto[]): Promise<iptv.IconFileDto[] | null> =>
  invokeHubConnection<iptv.IconFileDto[]>('GetIconsSimpleQuery', argument);

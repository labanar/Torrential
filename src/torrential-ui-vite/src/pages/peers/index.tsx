import { useCallback, useEffect } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSeedling } from "@fortawesome/free-solid-svg-icons";
import Layout from "../layout";
import { selectAllConnectedPeers, useAppDispatch } from "../../store";
import { useSelector } from "react-redux";
import { TorrentsState, setTorrents } from "../../store/slices/torrentsSlice";
import { setPeers } from "../../store/slices/peersSlice";
import {
  fetchTorrents as fetchTorrentsApi,
  PeerApiModel,
  TorrentApiModel,
} from "../../services/api";
import { PeerSummary, TorrentSummary } from "../../types";
import { Badge } from "@/components/ui/badge";
import { ScrollArea } from "@/components/ui/scroll-area";

export default function PeersPage() {
  return (
    <Layout>
      <Page />
    </Layout>
  );
}

function Page() {
  const dispatch = useAppDispatch();
  const peers = useSelector(selectAllConnectedPeers);

  const hydrate = useCallback(async () => {
    try {
      const data = await fetchTorrentsApi();

      const mappedTorrents = data.reduce((pv: TorrentsState, cv: TorrentApiModel) => {
        const summary: TorrentSummary = {
          name: cv.name,
          infoHash: cv.infoHash,
          progress: cv.progress,
          uploadRate: cv.uploadRate,
          downloadRate: cv.downloadRate,
          sizeInBytes: cv.totalSizeBytes,
          status: cv.status,
          bytesUploaded: cv.bytesUploaded,
          bytesDownloaded: cv.bytesDownloaded,
        };
        pv[cv.infoHash] = summary;
        return pv;
      }, {});
      dispatch(setTorrents(mappedTorrents));

      data.forEach((torrent: TorrentApiModel) => {
        const torrentPeers: PeerSummary[] = torrent.peers.reduce(
          (tpv: PeerSummary[], p: PeerApiModel) => {
            const summary: PeerSummary = {
              infoHash: torrent.infoHash,
              ip: p.ipAddress,
              port: p.port,
              peerId: p.peerId,
              isSeed: p.isSeed,
            };
            tpv.push(summary);
            return tpv;
          },
          []
        );
        dispatch(setPeers({ infoHash: torrent.infoHash, peers: torrentPeers }));
      });
    } catch {
      console.log("error fetching torrents");
    }
  }, [dispatch]);

  useEffect(() => {
    hydrate();
  }, [hydrate]);

  return (
    <div className="mx-auto flex h-full w-full max-w-6xl min-h-0 flex-col gap-4 overflow-hidden p-4 md:p-6">
      <header className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold tracking-tight">Peers</h1>
        <Badge variant="secondary">{peers.length} connected</Badge>
      </header>

      <section className="min-h-0 flex-1 overflow-hidden rounded-xl border border-border/70 bg-card/60">
        {peers.length === 0 ? (
          <div className="flex h-full items-center justify-center rounded-md border border-dashed text-sm text-muted-foreground">
            No connected peers
          </div>
        ) : (
          <ScrollArea className="h-full">
            <div className="divide-y divide-border/70">
              {peers.map((peer) => (
                <div
                  key={`${peer.infoHash}-${peer.peerId}`}
                  className="grid gap-3 px-4 py-3 transition-colors hover:bg-muted/40 sm:grid-cols-2 lg:grid-cols-4"
                >
                  <InfoPair label="IP" value={peer.ip} />
                  <InfoPair label="Port" value={`${peer.port}`} />
                  <InfoPair label="Torrent" value={peer.torrentName} />
                  <div className="flex items-center gap-2 text-sm">
                    <span className="text-muted-foreground">Seed</span>
                    {peer.isSeed ? (
                      <Badge variant="outline" className="gap-1 text-emerald-500">
                        <FontAwesomeIcon icon={faSeedling} /> Yes
                      </Badge>
                    ) : (
                      <Badge variant="outline">No</Badge>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </ScrollArea>
        )}
      </section>
    </div>
  );
}

function InfoPair({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="truncate text-sm">{value}</p>
    </div>
  );
}

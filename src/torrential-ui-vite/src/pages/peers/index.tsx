import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faUsers } from "@fortawesome/free-solid-svg-icons";
import Layout from "../layout";
import styles from "./peers.module.css";

export default function PeersPage() {
  return (
    <Layout>
      <div className={`${styles.root} page-shell`}>
        <div className={styles.pageHeader}>
          <h1 className={styles.pageTitle}>Peers</h1>
        </div>
        <div className={styles.emptyState}>
          <FontAwesomeIcon icon={faUsers} size="2x" className={styles.emptyStateIcon} />
          <p className={styles.emptyStateTitle}>No peer data available</p>
          <p className={styles.emptyStateDescription}>Peer information will appear here when torrents are active</p>
        </div>
      </div>
    </Layout>
  );
}

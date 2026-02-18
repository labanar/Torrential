import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlug } from "@fortawesome/free-solid-svg-icons";
import Layout from "../layout";
import styles from "./integrations.module.css";

export default function IntegrationsPage() {
  return (
    <Layout>
      <div className={`${styles.root} page-shell`}>
        <div className={styles.pageHeader}>
          <h1 className={styles.pageTitle}>Integrations</h1>
        </div>
        <div className={styles.emptyState}>
          <FontAwesomeIcon icon={faPlug} size="2x" className={styles.emptyStateIcon} />
          <p className={styles.emptyStateTitle}>No integrations configured</p>
          <p className={styles.emptyStateDescription}>Connect external services to extend Torrential</p>
        </div>
      </div>
    </Layout>
  );
}

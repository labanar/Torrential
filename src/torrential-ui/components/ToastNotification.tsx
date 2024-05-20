import React, { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import {
  Box,
  CloseButton,
  Text,
  ToastId,
  UseToastOptions,
  useToast,
} from "@chakra-ui/react";
import { RootState } from "@/app/store";
import { ToastNotification, dequeueNext } from "@/features/notificationsSlice";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import styles from "./ToastNotification.module.css";
import { type IconDefinition } from "@fortawesome/free-solid-svg-icons";

export const selectCurrentToast = (state: RootState) =>
  state.notifications.currentToast;

const renderToast = (
  id: ToastId,
  onClose: () => void,
  notification: ToastNotification
) => {
  const { status, icon, title, description } = notification;
  let bgColor = "blue.300";
  if (status === "success") bgColor = "green.300";
  if (status === "warning") bgColor = "orange.300";
  if (status === "error") bgColor = "red.300";
  return (
    <Box bg={bgColor} id={id.toString()} className={styles.toastContainer}>
      <Box className={styles.icon}>
        {typeof icon !== "string" && icon !== undefined && (
          <FontAwesomeIcon icon={icon as IconDefinition} size={"xl"} />
        )}
      </Box>
      <Box className={styles.content}>
        <Box className={styles.title}>
          <Text size={"sm"}>{title}</Text>
        </Box>
        <Box className={styles.description}>
          <Text size={"sm"}>{description}</Text>
        </Box>
      </Box>
      <Box className={styles.close}>
        <CloseButton onClick={onClose} />
      </Box>
    </Box>
  );
};

const ToastNotifications = () => {
  const dispatch = useDispatch();
  const toast = useToast();
  const currentToast = useSelector(selectCurrentToast);

  useEffect(() => {
    dispatch(dequeueNext());
  }, [dispatch]);

  useEffect(() => {
    if (currentToast) {
      const { duration } = currentToast;

      toast({
        position: "bottom-right",
        duration,
        render: ({ id, onClose }) => renderToast(id, onClose, currentToast),
      });

      dispatch(dequeueNext());
    }
  }, [currentToast, toast, dispatch]);

  return <></>;
};

export default ToastNotifications;

import {
  Divider,
  Input,
  Modal,
  ModalBody,
  ModalContent,
  ModalOverlay,
  Text,
} from "@chakra-ui/react";
import {
  IconDefinition,
  faArrowsUpDown,
  faGear,
  faPeopleGroup,
  faPlug,
  faUpDown,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import classNames from "classnames";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useHotkeys, useHotkeysContext } from "react-hotkeys-hook";
import styles from "./Aldred.module.css";
import { AlfredContext } from "@/features/alfredSlice";
import { AppDispatch } from "@/app/store";
import { AppRouterInstance } from "next/dist/shared/lib/app-router-context.shared-runtime";
import { useAppDispatch } from "@/app/hooks";
import { stringify } from "querystring";

interface AlfredProps {
  isOpen: boolean;
  close: () => void;
}

export default function Alfred({ isOpen, close }: AlfredProps) {
  const { enableScope, disableScope, enabledScopes } = useHotkeysContext();

  const router = useRouter();
  const dispatch = useAppDispatch();

  const [selectedId, setSelectedId] = useState(-1);
  const [suggestions, setSuggestions] = useState(globalSuggestions);
  const [scopesToEnableOnClose, setScopesToEnableOnClose] = useState([]);

  useEffect(() => {
    setSelectedId(-1);
  }, [suggestions]);

  useEffect(() => {
    if (isOpen) {
      setScopesToEnableOnClose(enabledScopes);
      enabledScopes.forEach((s) => disableScope(s));
      enableScope("search");
    } else {
      disableScope("search");
      scopesToEnableOnClose.forEach((s) => {
        enableScope(s);
      });
      setSelectedId(-1);
    }
    return () => disableScope("search"); // Clean up on unmount
  }, [isOpen]);

  useHotkeys(
    "up",
    () => {
      let nextId = selectedId - 1;
      if (nextId < 0) nextId = suggestions.length - 1;
      setSelectedId(nextId);
      console.log(nextId);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "down",
    () => {
      let nextId = selectedId + 1;
      if (nextId > suggestions.length - 1) nextId = nextId - suggestions.length;
      setSelectedId(nextId);
      console.log(nextId);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "enter",
    () => {
      if (selectedId >= 0 && suggestions.length > selectedId) {
        const suggestion = suggestions.at(selectedId);
        suggestion.action({ dispatch, router });
        close();
      }
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  return (
    <Modal isOpen={isOpen} onClose={() => {}} size={"xl"}>
      <ModalOverlay bg={"none"} />
      <ModalContent mt={"16%"}>
        <div>
          <Input
            placeholder="Type to search"
            border={0}
            height={14}
            focusBorderColor="none"
            _focus={{ boxShadow: "none" }}
            onKeyDown={(e) => {
              if (e.key === "Escape") {
                e.preventDefault();
              }
            }}
          />
        </div>
        <Divider />

        <ModalBody p={0}>
          {suggestions.map((s, i) => (
            <SearchSuggestion
              selected={selectedId === i}
              icon={s.icon}
              title={s.title}
            />
          ))}
        </ModalBody>
      </ModalContent>
    </Modal>
  );
}

interface SearchSuggestionProps {
  selected: boolean;
  icon?: IconDefinition;
  title: string;
}

function SearchSuggestion({ selected, icon, title }: SearchSuggestionProps) {
  return (
    <>
      <div
        className={classNames(styles.suggestion, {
          [styles.selected]: selected,
        })}
      >
        <FontAwesomeIcon icon={icon} size="xl" width={"28px"} />
        <Text fontSize={14}>{title}</Text>
      </div>
    </>
  );
}

interface SearchSuggestion {
  context: AlfredContext;
  icon?: IconDefinition;
  title: string;
  action: (actionProps: SearchSuggestionActionProps) => void;
}

interface SearchSuggestionActionProps {
  dispatch: AppDispatch;
  router: AppRouterInstance;
}

const globalSuggestions: SearchSuggestion[] = [
  {
    context: AlfredContext.Global,
    icon: faUpDown,
    title: "Torrents",
    action: ({ router }) => router.push("/"),
  },
  {
    context: AlfredContext.Global,
    icon: faPeopleGroup,
    title: "Peers",
    action: ({ router }) => router.push("/peers"),
  },
  {
    context: AlfredContext.Global,
    icon: faPlug,
    title: "Integrations",
    action: ({ router }) => router.push("/integrations"),
  },
  {
    context: AlfredContext.Global,
    icon: faGear,
    title: "Settings",
    action: ({ router }) => router.push("/settings"),
  },
];

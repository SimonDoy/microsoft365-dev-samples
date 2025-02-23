export interface INavigationProps {
  manager: boolean;
  setView: (view: string) => void;
  view: string;
  setIsChecked: (view: boolean) => void;
  refresh: boolean;
  setRefresh: (view: boolean) => void;
}

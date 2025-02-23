import * as React from 'react';
import { Spinner } from '@fluentui/react/lib/Spinner';
import { Dropdown, Label, PrimaryButton, Toggle } from '@fluentui/react';
import { IIconProps } from '@fluentui/react/lib/Icon';
import {  Field, FluentProvider, webLightTheme, } from '@fluentui/react-components';
import styles from '../../myTasks/components/MyTasks.module.scss';
import { FormikHandlers, FormikProps, useFormik } from 'formik';
import * as yup from 'yup';
import { ReadReceiptTaskService } from '../../myTasks/service/readReceiptTaskService';
import { CompleteReadReceiptTaskResponse } from '../../../models/completeReadReceiptTask';

interface CompleteReadReceiptTaskProps {
  taskService: ReadReceiptTaskService;
  taskId: string;
}


const CompleteReadReceiptTask: React.FC<CompleteReadReceiptTaskProps> = ({ taskService, taskId }) => {
  const validate = yup.object().shape({
    understandingLevel: yup.string().required('Understanding level is required'),
    hasReadContent: yup.bool()
      .required('Please confirm you have read the content.'),
  });

  const [task, setTask] = React.useState<CompleteReadReceiptTaskResponse>();
  const [errorMessage, setErrorMessage] = React.useState<string>();
  const [loading, setLoading] = React.useState(true);
  const cancelIcon: IIconProps = { iconName: 'Cancel' };
  const saveIcon: IIconProps = { iconName: 'Save' };
  
  const handleCompleteTask = async (values: CompleteReadReceiptTaskResponse): Promise<void> => {
    try 
    { 
      if(values.confirmationDate === undefined || values.confirmationDate === null){
        values.confirmationDate = new Date();
      }
      const completeTaskResponse = await taskService.completePlannerTask(taskId, values);
      console.log('response', completeTaskResponse);
      alert('Completed task');
    }
    catch(error)
    {
      console.error('Error completing task: ', error);
      setErrorMessage(error.message);
      alert('Failed to complete task');

    } 
    finally {
      setLoading(false);
    }
  };

  const formik = useFormik({
    initialValues: {
      id: task?.id,
      description: task?.description,
      externalId: task?.externalId,
      userPrincipalName: task?.userPrincipalName,
      understandingLevel: task?.understandingLevel,
      hasReadContent: task?.hasReadContent,
      confirmationDate: task?.confirmationDate,
      completionNotes: task?.completionNotes,
      percentComplete: task?.percentComplete,
      contentTitle: task?.contentTitle,
      contentUrl: task?.contentUrl
    },
    enableReinitialize:true,

    validationSchema: validate,
    onSubmit: async (values: CompleteReadReceiptTaskResponse) => {
      try{
        const response = await handleCompleteTask(values);
        console.log('response', response);
      }
      catch (error) {
        console.error('Issue submitting task completion', error);
      }
      
    },
  });

  React.useEffect(() => {

    const fetchData = async (): Promise<void>  => {
      setLoading(true);
      try {
        console.log("Task ID: ", taskId);
        const localTask = await taskService.getPlannerTask(taskId);
        console.log("Task: ", localTask);
        setTask(localTask);
        formik.resetForm({ values: localTask });
        if(localTask.percentComplete === 100){
          alert('This task has already been completed');
          setErrorMessage('This task has already been completed');
        }
      } catch (error) {
        console.error("Error loading tasks: ", error);
        setErrorMessage(error.message ? error.message : error)
      } finally {
        setLoading(false); // Set loading to false when done
      }
    };

    fetchData().then().catch(e=>console.error(e));

  }, [taskService]); 

  type GetFieldPropTuple = {
    errorMessage: string;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    value: any;
    name: string;
    multiple?: boolean;
    checked?: boolean;
    onChange: FormikHandlers["handleChange"];
    onBlur: FormikHandlers["handleBlur"];
  };
  
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const getFieldProps = (formik: FormikProps<any>, field: string) : GetFieldPropTuple => {
    const returnValue = { ...formik.getFieldProps(field), errorMessage: formik.errors[field] as string }
    return returnValue;
  };

  

  return (
      <FluentProvider theme={webLightTheme}>
        <div className={styles.container}>
          {loading ? (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%' }}>
              <Spinner label="Loading tasks..." ariaLive="assertive" labelPosition="right" />
            </div>
          ) : (
            errorMessage && errorMessage.length > 0 ? (
              <div>{errorMessage}</div>
            ) : (
            <form style={{ backgroundColor: '#ededed', padding:'5px'}} onSubmit={formik.handleSubmit}>
              <div>
                <div>
                  <Field label="Task Id">
                    <Label>{taskId}</Label>
                  </Field>
                  <Field label="Title">
                    <Label>{task?.contentTitle}</Label>
                  </Field>
                  
                  <Field label="Description">
                    <Label> {task?.description} </Label>
                  </Field>
                  <Field label="Percent complete">
                    <Label>{task?.percentComplete}%</Label>
                  </Field>
                
                  {
                    (task?.contentUrl && task?.contentUrl.length > 0) ? (
                    <Field label="Content to read">
                      <a href={task?.contentUrl} target="_blank" rel="noreferrer">{task?.contentTitle}</a>
                    </Field>
                    ) : null
                  }
                
                  <Toggle 
                    onChange={async (event, checked) => {await formik.setFieldValue('hasReadContent', checked?.valueOf());}} 
                    label="I confirm I have read the content of this task and understand the requirements." />

                  <Label>Level of understanding</Label>
                  <Dropdown 
                  options={[
                    { key: '', text: '' },
                    { key: 'I fully understand the content', text: 'I fully understand the content' },
                    { key: 'I do not understand some of the content', text: 'I do not understand some of the content' },
                    { key: 'I do not understand any of the content', text: 'I do not understand any of the content' }
                  ]}
                  {...getFieldProps(formik, 'understandingLevel')}
                  onChange={async (event, option)=> { await formik.setFieldValue('understandingLevel', option?.key.toString());}} 
                  placeholder="Select an option" />
                </div>
                <div>
                <br/>
                <PrimaryButton
                  type="submit"
                  text="Save"
                  iconProps={saveIcon}
                  disabled={(formik.isSubmitting || !formik.isValid)}
                />
                &nbsp;
                <PrimaryButton
                  text="Cancel"
                  iconProps={cancelIcon}
                  onClick={formik.handleReset}
                />
                </div>
              </div>
            </form>
          )
          )}
        </div>
      </FluentProvider>
  );

};

export default CompleteReadReceiptTask;

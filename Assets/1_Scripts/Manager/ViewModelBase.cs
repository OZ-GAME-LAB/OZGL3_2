using System;
using System.ComponentModel;

/*
public enum DNContainerEventType
{
    None,
    Add, // 요소가 추가된 경우
    Remove, // 요소가 제거된 경우
    Update, // 요소가 업데이트 된 경우
}

public interface IDNContainerPropertyChanged<T>
{ 
    // PropertyName / Type=Add,Remove,Update / T는 자식의 뷰모델 외에도 string이나, long ID같은걸 줄수도 있다!
    event Action<string, DNContainerEventType, T> ContainerPropertyChanged;
}
*/
public class ViewModelBase : INotifyPropertyChanged
{
    // INotifyPropertyChanged를 쓸려면 꼭 필요한 부분을 Base로 그냥 통합
    // Model 구현할 때 이걸 상속받으면 끝

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
